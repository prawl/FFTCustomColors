using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Tracks battle state by reading the turn queue for stats and the static battle
    /// array at 0x140893C00 (stride 0x200) for positions and real-time HP changes.
    /// Polls the static array every 100ms to detect damage, movement, and kills.
    /// </summary>
    public class BattleTracker : IDisposable
    {
        private readonly MemoryExplorer _explorer;
        private CancellationTokenSource? _cts;
        private readonly object _lock = new();

        // Memory addresses for turn queue and battle flags
        // NOTE: Position data for the active unit appears at a fixed offset in the
        // condensed turn queue. When a new unit rotates to slot 0, their full data
        // (including position) is written there. The position offset (+0xBC from queue base)
        // corresponds to the data AFTER the last ability list terminator for slot 0.
        // This works because the game writes the entire active unit's struct into slot 0.
        private const long AddrActiveUnitX = 0x14077D360; // uint16 LE, active unit X
        private const long AddrActiveUnitY = 0x14077D362; // uint16 LE, active unit Y
        private const long AddrTurnQueueBase = 0x14077D2A0;
        private const long AddrBattleTeam = 0x14077D2A2;
        private const long AddrBattleActed = 0x14077CA8C;
        private const long AddrBattleMoved = 0x14077CA9C;
        private const long AddrUnitSlot0 = 0x14077CA30;
        private const long AddrUnitSlot9 = 0x14077CA54;
        private const long AddrMenuCursor = 0x1407FC620;
        private const long AddrActiveNameId = 0x14077CA94;
        private const long AddrActiveCt = 0x14077CA60;
        private const long AddrActiveJobId = 0x14077CA6C;
        private const long AddrCondensedNameId = 0x14077D2A4; // condensed +0x04, matches roster nameId

        // Roster layout for nameId→slot lookup
        private const long AddrRosterBase = 0x1411A18D0;
        private const int RosterStride = 0x258;
        private const int RosterMaxSlots = 20;
        private const int RosterOffNameId = 0x230; // uint16
        private const int RosterOffBrave = 0x1E;   // byte
        private const int RosterOffFaith = 0x1F;   // byte
        private const int RosterOffReaction = 0x08; // byte, ability ID
        private const int RosterOffSupport = 0x0A;  // byte, ability ID
        private const int RosterOffMovement = 0x0C; // byte, ability ID
        private const int RosterOffEquipStart = 0x0E; // 7 x uint16 equipment IDs (0xFF=empty)

        // Turn queue field offsets (uint16 LE)
        private const int TqLevel = 0x00;
        private const int TqTeam = 0x02;
        private const int TqHp = 0x0C;
        private const int TqMaxHp = 0x10;
        private const int TqMp = 0x12;
        private const int TqMaxMp = 0x16;

        // Static battle array constants (discovered 2026-04-12)
        private const long BattleArrayBase = 0x140893C00;
        private const int ArrayStride = 0x200;
        private const int ArraySlotsBack = 20;  // enemies at negative offsets
        private const int ArraySlotsForward = 10; // players at positive offsets
        private const int ArrayTotalSlots = ArraySlotsBack + ArraySlotsForward;

        // Tracked state
        private readonly Dictionary<int, BattleUnit> _units = new();
        private bool _inBattle;
        private bool _pollDiagLogged;

        // Static array tracking for real-time HP/position change detection
        private readonly TrackedSlot[] _trackedSlots = new TrackedSlot[ArrayTotalSlots];
        private readonly List<BattleEvent> _recentEvents = new();
        private readonly object _eventsLock = new();

        public BattleTracker(MemoryExplorer explorer)
        {
            _explorer = explorer;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            Task.Run(async () =>
            {
                ModLogger.Log("[BattleTracker] Background polling started");
                while (!token.IsCancellationRequested)
                {
                    try { Update(); }
                    catch (Exception ex) { ModLogger.LogDebug($"[BattleTracker] Poll error: {ex.Message}"); }
                    await Task.Delay(100, token);
                }
            }, token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        public void Dispose() => Stop();

        public BattleState? Update()
        {
            if (_explorer == null) return null;
            lock (_lock) { return UpdateInternal(); }
        }

        private BattleState? UpdateInternal()
        {
            var reads = _explorer.ReadMultiple(new (nint, int)[]
            {
                ((nint)AddrUnitSlot0, 4),
                ((nint)AddrUnitSlot9, 4),
            });
            // slot9=0xFFFFFFFF is the reliable battle indicator. slot0 can flicker
            // from 255 during attack animations, so don't require it for staying in battle.
            // Use _inBattle as a sticky flag: enter on both slots, exit only when slot9 changes.
            bool enterBattle = reads[0] == 255 && reads[1] == 0xFFFFFFFF;
            bool inBattle = enterBattle || (_inBattle && reads[1] == 0xFFFFFFFF);
            if (!_inBattle && inBattle)
                ModLogger.Log($"[BattleTracker] Entering battle: slot0={reads[0]:X}, slot9={reads[1]:X}");
            if (_inBattle && !inBattle)
                ModLogger.Log($"[BattleTracker] Leaving battle: slot0={reads[0]:X}, slot9={reads[1]:X}");

            if (!inBattle)
            {
                if (_inBattle)
                {
                    _units.Clear();
                    _inBattle = false;
                    // Clear tracked slots and events on battle end
                    for (int i = 0; i < _trackedSlots.Length; i++)
                        _trackedSlots[i] = default;
                    lock (_eventsLock) { _recentEvents.Clear(); }
                }
                return null;
            }

            _inBattle = true;

            // Poll static battle array for real-time HP/position changes
            try { PollStaticArray(); }
            catch (Exception ex) { ModLogger.LogDebug($"[BattleTracker] PollStaticArray error: {ex.Message}"); }

            // Read turn queue slot 0 for active unit info + position from condensed struct
            var battleReads = _explorer.ReadMultiple(new (nint, int)[]
            {
                ((nint)AddrTurnQueueBase + TqLevel, 2),  // 0: level
                ((nint)AddrBattleTeam, 2),               // 1: team
                ((nint)AddrTurnQueueBase + TqHp, 2),     // 2: HP
                ((nint)AddrTurnQueueBase + TqMaxHp, 2),  // 3: maxHP
                ((nint)AddrTurnQueueBase + TqMp, 2),     // 4: MP
                ((nint)AddrTurnQueueBase + TqMaxMp, 2),  // 5: maxMP
                ((nint)AddrBattleActed, 1),              // 6: acted
                ((nint)AddrBattleMoved, 1),              // 7: moved
                ((nint)AddrMenuCursor, 1),               // 8: menu cursor
                ((nint)AddrActiveUnitX, 2),              // 9: active unit X (from condensed struct)
                ((nint)AddrActiveUnitY, 2),              // 10: active unit Y
                ((nint)AddrActiveNameId, 4),             // 11: nameId (battle state, different numbering)
                ((nint)AddrActiveCt, 4),                 // 12: ct
                ((nint)AddrActiveJobId, 4),              // 13: jobId
                ((nint)AddrCondensedNameId, 2),          // 14: condensed nameId (matches roster)
            });

            int activeLevel = (int)battleReads[0];
            int activeTeam = (int)battleReads[1];
            int activeHp = (int)battleReads[2];
            int activeMaxHp = (int)battleReads[3];
            int activeMp = (int)battleReads[4];
            int activeMaxMp = (int)battleReads[5];
            int acted = (int)battleReads[6];
            int moved = (int)battleReads[7];
            int menuCursor = (int)battleReads[8];
            int activeUnitX = (int)battleReads[9];
            int activeUnitY = (int)battleReads[10];
            int activeNameId = (int)battleReads[11];
            int activeCt = (int)battleReads[12];
            int activeJobId = (int)battleReads[13];
            int condensedNameId = (int)battleReads[14];

            // Lookup roster data by matching condensed nameId to roster nameIds
            int activeBrave = 0, activeFaith = 0;
            int activeReaction = 0, activeSupport = 0, activeMovement = 0;
            string? reactionName = null, supportName = null, movementName = null;
            var equipment = new List<int>();

            if (activeTeam == 0 && condensedNameId > 0) // only for player units
            {
                // Build batch read for all roster nameIds
                var nameIdReads = new (nint, int)[RosterMaxSlots];
                for (int s = 0; s < RosterMaxSlots; s++)
                    nameIdReads[s] = ((nint)(AddrRosterBase + s * RosterStride + RosterOffNameId), 2);
                var nameIds = _explorer.ReadMultiple(nameIdReads);

                for (int slot = 0; slot < RosterMaxSlots; slot++)
                {
                    if ((int)nameIds[slot] != condensedNameId) continue;

                    long slotAddr = AddrRosterBase + slot * RosterStride;
                    var rosterReads = _explorer.ReadMultiple(new (nint, int)[]
                    {
                        ((nint)(slotAddr + RosterOffBrave), 1),     // 0
                        ((nint)(slotAddr + RosterOffFaith), 1),     // 1
                        ((nint)(slotAddr + RosterOffReaction), 1),  // 2
                        ((nint)(slotAddr + RosterOffSupport), 1),   // 3
                        ((nint)(slotAddr + RosterOffMovement), 1),  // 4
                        ((nint)(slotAddr + RosterOffEquipStart + 0), 2),  // 5: equip slot 0
                        ((nint)(slotAddr + RosterOffEquipStart + 2), 2),  // 6: equip slot 1
                        ((nint)(slotAddr + RosterOffEquipStart + 4), 2),  // 7: equip slot 2
                        ((nint)(slotAddr + RosterOffEquipStart + 6), 2),  // 8: equip slot 3
                        ((nint)(slotAddr + RosterOffEquipStart + 8), 2),  // 9: equip slot 4
                        ((nint)(slotAddr + RosterOffEquipStart + 10), 2), // 10: equip slot 5
                        ((nint)(slotAddr + RosterOffEquipStart + 12), 2), // 11: equip slot 6
                    });
                    activeBrave = (int)rosterReads[0];
                    activeFaith = (int)rosterReads[1];
                    activeReaction = (int)rosterReads[2];
                    activeSupport = (int)rosterReads[3];
                    activeMovement = (int)rosterReads[4];

                    reactionName = AbilityData.GetAbility((byte)activeReaction)?.Name;
                    supportName = AbilityData.GetAbility((byte)activeSupport)?.Name;
                    movementName = AbilityData.GetAbility((byte)activeMovement)?.Name;

                    for (int e = 5; e <= 11; e++)
                    {
                        int eqId = (int)rosterReads[e];
                        if (eqId != 0xFF && eqId != 0xFFFF)
                            equipment.Add(eqId);
                    }
                    break;
                }
            }

            // Register active unit from turn queue and update position
            int activeKey = activeTeam * 100000 + activeLevel * 1000 + activeMaxHp;
            if (activeLevel > 0 && activeLevel <= 99 && activeMaxHp > 0)
            {
                if (!_units.TryGetValue(activeKey, out var unit))
                {
                    unit = new BattleUnit();
                    _units[activeKey] = unit;
                }
                unit.Team = activeTeam;
                unit.Level = activeLevel;
                unit.Hp = activeHp;
                unit.MaxHp = activeMaxHp;
                unit.Mp = activeMp;
                unit.MaxMp = activeMaxMp;

                // Update current position from condensed struct
                // This address updates when ANY unit rotates to slot 0
                if (activeUnitX >= 0 && activeUnitX <= 30 && activeUnitY >= 0 && activeUnitY <= 30)
                {
                    unit.X = activeUnitX;
                    unit.Y = activeUnitY;
                }
            }

            // Scan turn queue for other units' stats
            ScanTurnQueue();

            // Heap scanning disabled — causes slowdowns and crashes.
            // Live positions tracked via grid cursor + scan approach instead.

            // Build response
            var state = new BattleState
            {
                InBattle = true,
                ActiveUnit = activeLevel > 0 ? new ActiveUnitState
                {
                    Team = activeTeam,
                    Level = activeLevel,
                    Hp = activeHp,
                    MaxHp = activeMaxHp,
                    Mp = activeMp,
                    MaxMp = activeMaxMp,
                    Acted = acted == 1,
                    Moved = moved == 1,
                    HoveredAction = menuCursor switch
                    {
                        0 => "Move",
                        1 => "Abilities",
                        2 => "Wait",
                        3 => "Status",
                        4 => "AutoBattle",
                        _ => null
                    },
                    NameId = activeNameId,
                    Name = CharacterData.GetName(condensedNameId),
                    Ct = activeCt,
                    JobId = activeJobId,
                    Brave = activeBrave,
                    Faith = activeFaith,
                    ReactionAbility = activeReaction,
                    ReactionAbilityName = reactionName,
                    SupportAbility = activeSupport,
                    SupportAbilityName = supportName,
                    MovementAbility = activeMovement,
                    MovementAbilityName = movementName,
                    Equipment = equipment.Count > 0 ? equipment : null,
                } : null,
                Units = new List<BattleUnitState>(),
            };

            // Set active unit position from condensed struct (already read above)
            if (state.ActiveUnit != null)
            {
                if (activeUnitX >= 0 && activeUnitX <= 30 && activeUnitY >= 0 && activeUnitY <= 30)
                {
                    state.ActiveUnit.X = activeUnitX;
                    state.ActiveUnit.Y = activeUnitY;
                }
                else if (_units.TryGetValue(activeKey, out var activeUnit))
                {
                    state.ActiveUnit.X = activeUnit.X;
                    state.ActiveUnit.Y = activeUnit.Y;
                }
            }

            // Get active unit position for distance calculations
            int myX = state.ActiveUnit?.X ?? -1;
            int myY = state.ActiveUnit?.Y ?? -1;

            foreach (var kvp in _units)
            {
                var u = kvp.Value;
                var unitState = new BattleUnitState
                {
                    Team = u.Team,
                    Level = u.Level,
                    StartX = u.StartX,
                    StartY = u.StartY,
                    X = u.X,
                    Y = u.Y,
                    Hp = u.Hp,
                    MaxHp = u.MaxHp,
                    Mp = u.Mp,
                    MaxMp = u.MaxMp,
                    IsActive = kvp.Key == activeKey,
                    PositionKnown = u.X >= 0,
                    LifeState = u.Hp <= 0 && u.MaxHp > 0 ? "dead" : null,
                };

                // Calculate distance and cursor-key direction from active unit.
                // FFT isometric grid mapping (verified empirically):
                //   Down key  = X-1, Y-1 (visually forward)
                //   Up key    = X+1, Y+1 (visually backward)
                //   Left key  = X-1, Y+1 (visually left)
                //   Right key = X+1, Y-1 (visually right)
                if (myX >= 0 && myY >= 0 && u.X >= 0 && u.Y >= 0 && kvp.Key != activeKey)
                {
                    int dx = u.X - myX;
                    int dy = u.Y - myY;
                    unitState.Distance = Math.Abs(dx) + Math.Abs(dy);

                    // Map dx/dy to cursor key names
                    if (dx == 0 && dy == 0)
                        unitState.Direction = "same tile";
                    else if (dx <= 0 && dy <= 0)
                        unitState.Direction = "CursorDown";  // both decrease
                    else if (dx >= 0 && dy >= 0)
                        unitState.Direction = "CursorUp";    // both increase
                    else if (dx <= 0 && dy >= 0)
                        unitState.Direction = "CursorLeft";  // X dec, Y inc
                    else
                        unitState.Direction = "CursorRight"; // X inc, Y dec
                }

                state.Units.Add(unitState);
            }

            // 2026-04-26 Mandalia: scan responses leaked stale units
            // from prior battles (Lv25 phantoms on a Ch1 map) because
            // _units is keyed by team*lvl*MaxHp and only cleared on
            // battle exit detection — which can misfire. Build the
            // active MaxHp set from the static-array poll (which DOES
            // correctly filter inBattle) and drop _units entries whose
            // MaxHp doesn't appear there. Active unit always kept.
            var activeMaxHps = new HashSet<int>();
            for (int s = 0; s < _trackedSlots.Length; s++)
            {
                ref var slot = ref _trackedSlots[s];
                if (slot.Active && slot.MaxHp > 0) activeMaxHps.Add(slot.MaxHp);
            }
            if (activeMaxHps.Count > 0)
            {
                state.Units = StaleBattleUnitFilter.Filter(state.Units, activeMaxHps);
            }

            state.BattleWon = BattleFieldHelper.AllEnemiesDefeated(state.Units);

            return state;
        }

        /// <summary>
        /// Scan the turn queue to discover units and their stats (not positions).
        /// </summary>
        private void ScanTurnQueue()
        {
            var raw = _explorer.Scanner.ReadBytes((nint)AddrTurnQueueBase, 512);
            if (raw.Length < 20) return;

            int pos = 0;
            while (pos < raw.Length - 20)
            {
                if (pos < raw.Length - 1 && raw[pos] == 0xFF && raw[pos + 1] == 0xFF)
                {
                    pos += 2;
                    if (pos < raw.Length - 1 && raw[pos] == 0x00 && raw[pos + 1] == 0x00)
                        pos += 2;

                    if (pos < raw.Length - 24)
                    {
                        int lvl = BitConverter.ToUInt16(raw, pos);
                        int team = BitConverter.ToUInt16(raw, pos + 2);

                        if (lvl >= 1 && lvl <= 99 && team >= 0 && team <= 3)
                        {
                            int hp = BitConverter.ToUInt16(raw, pos + 0x0C);
                            int maxHp = BitConverter.ToUInt16(raw, pos + 0x10);
                            int mp = BitConverter.ToUInt16(raw, pos + 0x12);
                            int maxMp = BitConverter.ToUInt16(raw, pos + 0x16);

                            if (maxHp > 0 && maxHp < 10000)
                            {
                                int key = team * 100000 + lvl * 1000 + maxHp;
                                if (!_units.TryGetValue(key, out var unit))
                                {
                                    unit = new BattleUnit { X = -1, Y = -1 };
                                    _units[key] = unit;
                                }
                                unit.Team = team;
                                unit.Level = lvl;
                                unit.Hp = hp;
                                unit.MaxHp = maxHp;
                                unit.Mp = mp;
                                unit.MaxMp = maxMp;

                                // Brave/faith no longer needed for heap search
                                // (we search by MaxHP + level instead)
                            }

                            pos += 0x18;
                            continue;
                        }
                    }
                }
                pos++;
            }
        }

        private const long AddrMoveTileBase = 0x140C66315;

        /// <summary>
        /// Reads the active unit's current position from movement tile[0].
        /// Only valid during the active unit's turn when the tile list is populated.
        /// </summary>
        private (int x, int y)? ReadActiveTilePosition()
        {
            try
            {
                var tileBytes = _explorer.Scanner.ReadBytes((nint)AddrMoveTileBase, 7);
                if (tileBytes.Length < 3) return null;
                int x = tileBytes[0];
                int y = tileBytes[1];
                // Sanity check
                if (x >= 0 && x <= 30 && y >= 0 && y <= 30)
                    return (x, y);
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Poll the static battle array for real-time HP/position changes.
        /// Called every 100ms from the Update loop. Detects damage, healing,
        /// movement, and death in real-time without any game input.
        /// </summary>
        private void PollStaticArray()
        {
            // Batch-read HP + MaxHP + gridX + gridY + inBattleFlag for all slots
            const int fields = 5;
            var reads = new (nint, int)[ArrayTotalSlots * fields];
            for (int s = 0; s < ArrayTotalSlots; s++)
            {
                long sb = BattleArrayBase + (long)(s - ArraySlotsBack + 1) * ArrayStride;
                reads[s * fields + 0] = ((nint)(sb + 0x14), 2); // HP
                reads[s * fields + 1] = ((nint)(sb + 0x16), 2); // MaxHP
                reads[s * fields + 2] = ((nint)(sb + 0x33), 1); // gridX
                reads[s * fields + 3] = ((nint)(sb + 0x34), 1); // gridY
                reads[s * fields + 4] = ((nint)(sb + 0x12), 2); // inBattleFlag
            }
            var sv = _explorer.ReadMultiple(reads);

            // One-time diagnostic: log first active slot found
            if (!_pollDiagLogged)
            {
                int activeCount = 0;
                for (int d = 0; d < ArrayTotalSlots; d++)
                {
                    int dFlag = (int)sv[d * fields + 4];
                    int dMaxHp = (int)sv[d * fields + 1];
                    if (dFlag != 0 && dMaxHp > 0 && dMaxHp < 2000) activeCount++;
                }
                ModLogger.Log($"[BattleTracker] PollStaticArray diagnostic: {activeCount} active slots out of {ArrayTotalSlots}");
                if (activeCount == 0)
                {
                    // Log first few raw values for debugging
                    for (int d = 0; d < 5 && d < ArrayTotalSlots; d++)
                        ModLogger.Log($"[BattleTracker]   slot[{d}]: hp={sv[d*fields]}, maxHp={sv[d*fields+1]}, gx={sv[d*fields+2]}, gy={sv[d*fields+3]}, flag={sv[d*fields+4]}");
                }
                _pollDiagLogged = true;
            }

            for (int s = 0; s < ArrayTotalSlots; s++)
            {
                int hp = (int)sv[s * fields + 0];
                int maxHp = (int)sv[s * fields + 1];
                int gx = (int)sv[s * fields + 2];
                int gy = (int)sv[s * fields + 3];
                int inBattle = (int)sv[s * fields + 4];

                ref var slot = ref _trackedSlots[s];

                // Skip non-battle or invalid slots
                if (inBattle == 0 || maxHp <= 0 || maxHp >= 2000 || gx > 30 || gy > 30)
                {
                    slot.Active = false;
                    continue;
                }

                if (!slot.Active || slot.MaxHp != maxHp)
                {
                    // New unit or unit changed — initialize tracking
                    slot.Active = true;
                    slot.Hp = hp;
                    slot.MaxHp = maxHp;
                    slot.GridX = gx;
                    slot.GridY = gy;
                    continue;
                }

                // Detect HP changes
                if (hp != slot.Hp)
                {
                    int delta = hp - slot.Hp;
                    string type = delta < 0 ? "damage" : "heal";
                    int amount = Math.Abs(delta);
                    bool died = hp <= 0 && slot.Hp > 0;

                    var ev = new BattleEvent
                    {
                        Type = died ? "kill" : type,
                        GridX = gx, GridY = gy,
                        Amount = amount,
                        HpBefore = slot.Hp, HpAfter = hp,
                        MaxHp = maxHp,
                        Timestamp = DateTime.UtcNow,
                    };

                    lock (_eventsLock) { _recentEvents.Add(ev); }

                    if (died)
                        ModLogger.Log($"[BattleTracker] KILL at ({gx},{gy}): {slot.Hp}→0/{maxHp} ({amount} damage)");
                    else if (delta < 0)
                        ModLogger.Log($"[BattleTracker] {amount} damage to ({gx},{gy}): {slot.Hp}→{hp}/{maxHp}");
                    else
                        ModLogger.Log($"[BattleTracker] {amount} healed at ({gx},{gy}): {slot.Hp}→{hp}/{maxHp}");

                    slot.Hp = hp;
                }

                // Detect position changes (unit moved)
                if (gx != slot.GridX || gy != slot.GridY)
                {
                    var ev = new BattleEvent
                    {
                        Type = "move",
                        GridX = gx, GridY = gy,
                        FromX = slot.GridX, FromY = slot.GridY,
                        MaxHp = maxHp,
                        Timestamp = DateTime.UtcNow,
                    };
                    lock (_eventsLock) { _recentEvents.Add(ev); }
                    ModLogger.Log($"[BattleTracker] Unit moved ({slot.GridX},{slot.GridY})→({gx},{gy}) hp={hp}/{maxHp}");
                    slot.GridX = gx;
                    slot.GridY = gy;
                }
            }

            // Trim old events (keep last 30 seconds)
            lock (_eventsLock)
            {
                var cutoff = DateTime.UtcNow.AddSeconds(-30);
                _recentEvents.RemoveAll(e => e.Timestamp < cutoff);
            }
        }

        /// <summary>Get recent battle events (damage, heals, moves, kills) since the given time.</summary>
        public List<BattleEvent> GetEventsSince(DateTime since)
        {
            lock (_eventsLock)
            {
                return _recentEvents.Where(e => e.Timestamp >= since).ToList();
            }
        }

        /// <summary>Get all recent battle events (last 30 seconds).</summary>
        public List<BattleEvent> GetRecentEvents()
        {
            lock (_eventsLock) { return _recentEvents.ToList(); }
        }

        private struct TrackedSlot
        {
            public bool Active;
            public int Hp, MaxHp;
            public int GridX, GridY;
        }

        public class BattleEvent
        {
            public string Type { get; set; } = ""; // "damage", "heal", "kill", "move"
            public int GridX { get; set; }
            public int GridY { get; set; }
            public int FromX { get; set; }  // for moves
            public int FromY { get; set; }  // for moves
            public int Amount { get; set; }  // damage/heal amount
            public int HpBefore { get; set; }
            public int HpAfter { get; set; }
            public int MaxHp { get; set; }
            public DateTime Timestamp { get; set; }
        }

        private class BattleUnit
        {
            public int Team;
            public int Level;
            public int StartX = -1;
            public int StartY = -1;
            public int X = -1;
            public int Y = -1;
            public int Hp;
            public int MaxHp;
            public int Mp;
            public int MaxMp;
        }
    }

    // JSON response models

    public class BattleState
    {
        [JsonPropertyName("inBattle")]
        public bool InBattle { get; set; }

        [JsonPropertyName("mapName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MapName { get; set; }

        [JsonPropertyName("mapId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int MapId { get; set; }

        [JsonPropertyName("battleWon")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool BattleWon { get; set; }

        [JsonPropertyName("activeUnit")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ActiveUnitState? ActiveUnit { get; set; }

        [JsonPropertyName("units")]
        public List<BattleUnitState> Units { get; set; } = new();

        [JsonPropertyName("turnOrder")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<TurnOrderEntry>? TurnOrder { get; set; }
    }

    public class TurnOrderEntry
    {
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; }

        [JsonPropertyName("team")]
        public string Team { get; set; } = "ENEMY";

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("hp")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Hp { get; set; }

        [JsonPropertyName("maxHp")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int MaxHp { get; set; }

        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("ct")]
        public int CT { get; set; }

        [JsonPropertyName("isActive")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsActive { get; set; }
    }

    public class ActiveUnitState
    {
        [JsonPropertyName("team")]
        public int Team { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("hp")]
        public int Hp { get; set; }

        [JsonPropertyName("maxHp")]
        public int MaxHp { get; set; }

        [JsonPropertyName("mp")]
        public int Mp { get; set; }

        [JsonPropertyName("maxMp")]
        public int MaxMp { get; set; }

        [JsonPropertyName("acted")]
        public bool Acted { get; set; }

        [JsonPropertyName("moved")]
        public bool Moved { get; set; }

        [JsonPropertyName("hoveredAction")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? HoveredAction { get; set; }

        [JsonPropertyName("nameId")]
        public int NameId { get; set; }

        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; }

        [JsonPropertyName("ct")]
        public int Ct { get; set; }

        [JsonPropertyName("jobId")]
        public int JobId { get; set; }

        [JsonPropertyName("jobName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? JobName { get; set; }

        // Roster-sourced fields (player units only, via nameId→roster lookup)

        [JsonPropertyName("brave")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Brave { get; set; }

        [JsonPropertyName("faith")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Faith { get; set; }

        [JsonPropertyName("reactionAbility")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int ReactionAbility { get; set; }

        [JsonPropertyName("reactionAbilityName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ReactionAbilityName { get; set; }

        [JsonPropertyName("supportAbility")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int SupportAbility { get; set; }

        [JsonPropertyName("supportAbilityName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SupportAbilityName { get; set; }

        [JsonPropertyName("movementAbility")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int MovementAbility { get; set; }

        [JsonPropertyName("movementAbilityName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MovementAbilityName { get; set; }

        [JsonPropertyName("equipment")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<int>? Equipment { get; set; }

        [JsonPropertyName("move")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Move { get; set; }

        [JsonPropertyName("jump")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Jump { get; set; }

        [JsonPropertyName("pa")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int PA { get; set; }

        [JsonPropertyName("ma")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int MA { get; set; }
    }

    public class BattleUnitState
    {
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; }

        [JsonPropertyName("team")]
        public int Team { get; set; }

        [JsonPropertyName("jobId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int JobId { get; set; }

        [JsonPropertyName("jobName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? JobName { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }

        /// <summary>Charge Time (0-100). Unit acts when CT reaches 100.</summary>
        [JsonPropertyName("ct")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int CT { get; set; }

        /// <summary>Speed stat (base, without equipment modifiers). Determines CT gain per tick.</summary>
        [JsonPropertyName("speed")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Speed { get; set; }

        /// <summary>Movement range (tiles per turn). Surface on enemies so Claude
        /// can judge closing threat without drilling into per-unit stats.</summary>
        [JsonPropertyName("move")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Move { get; set; }

        /// <summary>Jump range (elevation delta that movement can clear).</summary>
        [JsonPropertyName("jump")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Jump { get; set; }

        [JsonPropertyName("startX")]
        public int StartX { get; set; }

        [JsonPropertyName("startY")]
        public int StartY { get; set; }

        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        /// <summary>
        /// Display height of the unit's tile (Height + SlopeHeight/2). Lets
        /// the shell renderer summarize "you're on h=5, enemies on h≤2" so
        /// Claude can reason about high-ground positioning without having
        /// to cross-reference Move tiles.
        /// </summary>
        [JsonPropertyName("h")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public double H { get; set; }

        [JsonPropertyName("hp")]
        public int Hp { get; set; }

        [JsonPropertyName("maxHp")]
        public int MaxHp { get; set; }

        [JsonPropertyName("mp")]
        public int Mp { get; set; }

        [JsonPropertyName("maxMp")]
        public int MaxMp { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }

        [JsonPropertyName("positionKnown")]
        public bool PositionKnown { get; set; }

        /// <summary>Manhattan distance from active unit. -1 if position unknown.</summary>
        [JsonPropertyName("distance")]
        public int Distance { get; set; } = -1;

        /// <summary>Direction from active unit (e.g. "down-left", "up"). Empty if unknown.</summary>
        [JsonPropertyName("direction")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Direction { get; set; }

        /// <summary>Cardinal direction the unit is facing (N/S/E/W). Derived from last movement delta.</summary>
        [JsonPropertyName("facing")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Facing { get; set; }

        /// <summary>Life state: "alive", "dead" (can be raised), "crystal" or "treasure" (permanently gone).</summary>
        [JsonPropertyName("lifeState")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LifeState { get; set; }

        /// <summary>
        /// Turns remaining before a KO'd unit crystallizes and is permanently lost.
        /// 3 = just died (3 hearts), 2 = urgent, 1 = critical (next tick = crystal), 0 = crystallized.
        /// Null for alive units. Helps Claude prioritize: "do I Phoenix Down now or can I wait?"
        /// NOTE: not yet populated from memory — needs the death counter address discovered
        /// in a live session. See TODO.md.
        /// </summary>
        [JsonPropertyName("deathCounter")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int DeathCounter { get; set; }

        // === Equipped passive abilities ===
        // Player units: read from roster (+0x08/+0x0A/+0x0C byte IDs).
        // Enemy units: decoded from heap struct bitfields (+0x74 reaction, +0x78 support).
        // See BATTLE_MEMORY_MAP.md section 16 "Passive Ability Bitfields".

        /// <summary>Equipped reaction ability name (e.g. "Counter Tackle", "Auto-Potion").</summary>
        [JsonPropertyName("reaction")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Reaction { get; set; }

        /// <summary>Equipped support ability name (e.g. "Dual Wield", "Concentration").</summary>
        [JsonPropertyName("support")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Support { get; set; }

        /// <summary>Equipped movement ability name (e.g. "Move+2", "Teleport").</summary>
        [JsonPropertyName("movement")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Movement { get; set; }

        /// <summary>
        /// Pre-computed weapon tag for the active-unit banner: "{WeaponName}"
        /// or "{WeaponName} onHit:{AttackEffects}" from ItemData.ComposeWeaponTag.
        /// Null/empty when unarmed or unknown. Populated server-side so the shell
        /// can render `[BattleMyTurn] Ramza(Gallant Knight) [Chaos Blade onHit:...] (2,1)`
        /// without needing to plumb the full equipment list through the wire.
        /// </summary>
        [JsonPropertyName("weaponTag")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? WeaponTag { get; set; }

        /// <summary>
        /// Comma-separated list of non-weapon equipment names (shield, helm,
        /// body, accessory) from ItemData.ComposeEquipmentTag. Lets the
        /// active-unit line surface defensive loadout next to the weapon
        /// tag so Claude can see "Crystal Mail + Genji Shield" at a glance.
        /// Null/empty when unarmored.
        /// </summary>
        [JsonPropertyName("equipmentTag")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? EquipmentTag { get; set; }

        // === Elemental properties ===
        // Per-unit elemental resistances from equipment and innate traits.
        // Claude can see "Black Goblin [weak:Fire]" and prioritize Fire spells.
        // NOT YET POPULATED — needs heap struct addresses (PSX 0x6D-0x70).

        /// <summary>Elements this unit absorbs (healed instead of damaged). Null if none.</summary>
        [JsonPropertyName("elementAbsorb")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? ElementAbsorb { get; set; }

        /// <summary>Elements this unit nullifies (zero damage). Null if none.</summary>
        [JsonPropertyName("elementNull")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? ElementNull { get; set; }

        /// <summary>Elements this unit resists (half damage). Null if none.</summary>
        [JsonPropertyName("elementHalf")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? ElementHalf { get; set; }

        /// <summary>Elements this unit is weak to (double damage). Null if none.</summary>
        [JsonPropertyName("elementWeak")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? ElementWeak { get; set; }

        /// <summary>Elements this unit strengthens (own outgoing damage × 1.25).
        /// Session 30 live-verified via Gaia Gear + Kaiser Shield. Null if none.</summary>
        [JsonPropertyName("elementStrengthen")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? ElementStrengthen { get; set; }

        // === Charging/casting state ===
        // When a unit is charging a spell, shows the ability and remaining CT.
        // Claude needs this to: not issue commands to charging allies, know when
        // spells will fire, and decide whether to interrupt enemy casters.
        // NOT YET POPULATED — needs PSX 0x15D/0x170 equivalents.

        /// <summary>Name of the ability currently being charged/cast. Null if not charging.</summary>
        [JsonPropertyName("chargingAbility")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ChargingAbility { get; set; }

        /// <summary>Remaining CT ticks until the charged ability resolves. 0 if not charging.</summary>
        [JsonPropertyName("chargeCt")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int ChargeCt { get; set; }

        /// <summary>Active status effects on this unit (e.g. "Poison", "Haste", "Protect").</summary>
        [JsonPropertyName("statuses")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Statuses { get; set; }

        /// <summary>Learned action abilities this unit can use (excluding basic Attack).</summary>
        [JsonPropertyName("abilities")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<AbilityEntry>? Abilities { get; set; }

        /// <summary>Secondary skillset index from roster +0x07. 0 = none equipped.</summary>
        [JsonPropertyName("secondaryAbility")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int SecondaryAbility { get; set; }
    }

    public class AbilityEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("mp")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Mp { get; set; }

        [JsonPropertyName("horizontalRange")]
        public string HRange { get; set; } = "";

        [JsonPropertyName("verticalRange")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int VRange { get; set; }

        [JsonPropertyName("areaOfEffect")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int AoE { get; set; }

        [JsonPropertyName("heightOfEffect")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int HoE { get; set; }

        [JsonPropertyName("target")]
        public string Target { get; set; } = "";

        [JsonPropertyName("effect")]
        public string Effect { get; set; } = "";

        [JsonPropertyName("castSpeed")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int CastSpeed { get; set; }

        [JsonPropertyName("element")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Element { get; set; }

        [JsonPropertyName("addedEffect")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AddedEffect { get; set; }

        [JsonPropertyName("reflectable")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Reflectable { get; set; }

        [JsonPropertyName("arithmetickable")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Arithmetickable { get; set; }

        /// <summary>
        /// For consumable-backed abilities (Chemist Items / Ninja Throw /
        /// Samurai Iaido), the number of uses remaining based on the
        /// player's inventory count. Null for regular abilities (Fire,
        /// Cure, Attack) whose usage isn't gated on inventory.
        ///
        /// Chemist Items: count of that specific consumable (Potion=3).
        /// Samurai Iaido: count of the specific katana being drawn from.
        /// Ninja Throw: sum of all weapons of the throwable type.
        /// </summary>
        [JsonPropertyName("heldCount")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? HeldCount { get; set; }

        /// <summary>
        /// True when a consumable-backed ability has zero stock — Claude
        /// should skip it. Serialized only when true.
        /// </summary>
        [JsonPropertyName("unusable")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Unusable { get; set; }

        /// <summary>
        /// Valid target tiles for this ability. In compact mode (default), only
        /// tiles with occupants are included — use TotalTargets for the full count.
        /// In verbose mode, all tiles are included. Null for ineligible abilities.
        /// </summary>
        [JsonPropertyName("validTargetTiles")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ValidTargetTile>? ValidTargetTiles { get; set; }

        /// <summary>
        /// Total number of valid target tiles (including empty ones). Only
        /// populated in compact mode — lets Claude know the full range without
        /// listing every empty tile.
        /// </summary>
        [JsonPropertyName("totalTargets")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int TotalTargets { get; set; }

        /// <summary>
        /// For radius-AoE abilities only: the top-ranked aim centers by splash
        /// hit count, so Claude can pick the best placement without enumerating
        /// splashes for every valid center. Sorted by (enemies hit - allies hit)
        /// for enemy-target abilities, or by (allies hit) for ally-target ones.
        /// Omitted for point-target abilities (no ranking needed) and when no
        /// units are in splash range (nothing to rank).
        /// </summary>
        [JsonPropertyName("bestCenters")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SplashCenter>? BestCenters { get; set; }

        /// <summary>
        /// For line-shape abilities only (Shockwave, Divine Ruination): one
        /// entry per cardinal direction that catches at least one unit. Ranked
        /// like BestCenters. Claude picks a direction by clicking its seed tile.
        /// </summary>
        [JsonPropertyName("bestDirections")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<DirectionalHit>? BestDirections { get; set; }
    }

    /// <summary>
    /// A single tile in an ability's valid-target list, annotated with occupant
    /// info when a unit is standing on it. Empty tiles omit occupant/unitName.
    /// </summary>
    public class ValidTargetTile
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        /// <summary>"self", "ally", "enemy", or null for an empty tile.</summary>
        [JsonPropertyName("occupant")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Occupant { get; set; }

        /// <summary>Display name of the unit standing here, if any.</summary>
        [JsonPropertyName("unitName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UnitName { get; set; }

        /// <summary>
        /// Element-affinity marker relative to the ability's element. One of
        /// "absorb" / "null" / "half" / "weak" / "strengthen", or null when the
        /// ability is non-elemental, the tile is empty, or the occupant has no
        /// affinity for the ability's element. Populated by
        /// <see cref="ElementAffinityAnnotator"/> at scan time.
        /// </summary>
        [JsonPropertyName("affinity")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Affinity { get; set; }

        /// <summary>
        /// Attack arc from active unit to the occupant of this tile, relative
        /// to the occupant's facing. One of "front" / "side" / "back", or null
        /// when the tile is empty, the occupant's facing is unknown, or the
        /// attacker is on the same tile. Populated by
        /// <see cref="BackstabArcCalculator"/> at scan time. Use to prefer
        /// rear-arc attacks (higher hit%, crit bonus in FFT canon).
        /// </summary>
        [JsonPropertyName("arc")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Arc { get; set; }

        /// <summary>
        /// True when terrain obstructs a straight-line projectile from the
        /// caster to this tile. Null for non-projectile abilities (melee,
        /// self-target, AoE-centered), or when the path is clear. Populated
        /// by <see cref="LineOfSightCalculator"/> at scan time using the
        /// loaded map's display-height data.
        /// </summary>
        [JsonPropertyName("losBlocked")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? LosBlocked { get; set; }

        /// <summary>
        /// Tactical intent tag for revive abilities (Phoenix Down, Raise,
        /// Arise, Revive). One of:
        ///   "REVIVE"        — dead ally; canonical revive
        ///   "REVIVE-ENEMY!" — dead enemy; resurrects them as alive (bad)
        ///   "KO"            — undead-status enemy; reverse-revive kill
        ///   "KO-ALLY!"      — undead-status ally; kills your own unit (bad)
        /// Null on non-revive abilities or when the tile has no relevant
        /// occupant. Lets the shell renderer disambiguate Phoenix Down on a
        /// Skeleton (kill move) from Phoenix Down on a dead Goblin (would
        /// resurrect the enemy). See ReviveTargetClassifier.
        /// </summary>
        [JsonPropertyName("intent")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Intent { get; set; }
    }

    /// <summary>
    /// A ranked aim-center for a radius-AoE ability. Lists the units that would
    /// be caught in the splash if the ability were cast at this tile.
    /// </summary>
    public class SplashCenter
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        /// <summary>Display names of enemy units in the splash.</summary>
        [JsonPropertyName("enemies")]
        public List<string> Enemies { get; set; } = new();

        /// <summary>Display names of ally units (including the caster) in the splash.</summary>
        [JsonPropertyName("allies")]
        public List<string> Allies { get; set; } = new();

        /// <summary>
        /// Per-hit element-affinity markers, positionally aligned with Enemies[].
        /// Each entry is "absorb"/"null"/"half"/"weak"/"strengthen" or null.
        /// Populated only when the ability has an element AND the hit unit has a
        /// matching affinity. Null list = ability has no element.
        /// </summary>
        [JsonPropertyName("enemyAffinities")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string?>? EnemyAffinities { get; set; }

        /// <summary>Positionally aligned with Allies[]. Same semantics as EnemyAffinities.</summary>
        [JsonPropertyName("allyAffinities")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string?>? AllyAffinities { get; set; }
    }

    /// <summary>
    /// A ranked direction for a line-shape ability. Claude picks this direction
    /// by clicking the seed tile one step in front of the caster. Lists the
    /// units that would be hit by the line.
    /// </summary>
    public class DirectionalHit
    {
        /// <summary>Compass direction: "N", "E", "S", or "W".</summary>
        [JsonPropertyName("direction")]
        public string Direction { get; set; } = "";

        /// <summary>
        /// Seed tile coordinates — the cardinal neighbor Claude clicks to aim
        /// the line in this direction.
        /// </summary>
        [JsonPropertyName("seed")]
        public int[] Seed { get; set; } = new[] { 0, 0 };

        /// <summary>Display names of enemy units the line hits.</summary>
        [JsonPropertyName("enemies")]
        public List<string> Enemies { get; set; } = new();

        /// <summary>Display names of ally units the line hits.</summary>
        [JsonPropertyName("allies")]
        public List<string> Allies { get; set; } = new();

        /// <summary>Positionally aligned with Enemies[]. Per-hit affinity marker.</summary>
        [JsonPropertyName("enemyAffinities")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string?>? EnemyAffinities { get; set; }

        /// <summary>Positionally aligned with Allies[]. Per-hit affinity marker.</summary>
        [JsonPropertyName("allyAffinities")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string?>? AllyAffinities { get; set; }
    }
}
