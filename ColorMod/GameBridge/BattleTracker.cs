using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Tracks battle state by reading the turn queue for stats and scanning heap memory
    /// for unit positions. The IC remaster stores per-unit battle data in heap-allocated
    /// structs with this layout from the stat pattern start:
    ///   +0x00: exp(byte) level(byte) origBrave(byte) brave(byte) origFaith(byte) faith(byte)
    ///   +0x06: turnFlag(byte) 00
    ///   +0x08: HP(uint16) MaxHP(uint16) MP(uint16) pad MaxMP(uint16)
    ///   +0x1A: X position (byte)
    ///   +0x23: Y position (byte)
    /// Each unit has an "active" copy (turnFlag=1) and a "saved" copy (turnFlag=0),
    /// separated by stride 0x800. The active copy has live position data.
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

        // NOTE: Effective stats (Speed, PA, MA, Move, Jump) are only in the heap battle struct
        // or the variable-offset post-ability section. Omitted until heap scanning is reliable.

        // Heap battle struct offsets (from stat pattern start: exp level origBr br origFa fa)
        private const int HeapOffX = 0x1A;
        private const int HeapOffY = 0x23;
        private const int HeapOffTurnFlag = 0x06;
        private const int HeapOffHp = 0x08;
        private const int HeapOffMaxHp = 0x0A;
        private const int HeapOffMp = 0x0C;
        private const int HeapOffMaxMp = 0x0E;

        // Turn queue field offsets (uint16 LE)
        private const int TqLevel = 0x00;
        private const int TqTeam = 0x02;
        private const int TqHp = 0x0C;
        private const int TqMaxHp = 0x10;
        private const int TqMp = 0x12;
        private const int TqMaxMp = 0x16;

        // Tracked state
        private readonly Dictionary<int, BattleUnit> _units = new();
        private readonly Dictionary<int, nint> _heapAddresses = new(); // unitKey → heap struct address
        private bool _inBattle;
        private bool _heapScanDone;
        private DateTime _lastHeapScan = DateTime.MinValue;

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
                    catch { /* ignore */ }
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
            bool inBattle = reads[0] == 255 && reads[1] == 0xFFFFFFFF;

            if (!inBattle)
            {
                if (_inBattle)
                {
                    _units.Clear();
                    _heapAddresses.Clear();
                    _heapScanDone = false;
                    _inBattle = false;
                }
                return null;
            }

            _inBattle = true;

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

            state.BattleWon = BattleFieldHelper.AllEnemiesDefeated(state.Units);

            return state;
        }

        /// <summary>
        /// Scan heap memory for unit battle structs.
        /// Strategy: search for HP(u16) MaxHP(u16) pattern from the turn queue,
        /// then verify level byte at -0x08 and read X at +0x12, Y at +0x1B
        /// (offsets relative to HP field, which is at +0x08 from stat start).
        /// </summary>
        private void ScanHeapForPositions()
        {
            var sw = Stopwatch.StartNew();
            int found = 0;

            foreach (var kvp in _units)
            {
                var unit = kvp.Value;
                if (unit.Level <= 0 || unit.MaxHp <= 0) continue;

                // Check cached address first
                if (_heapAddresses.TryGetValue(kvp.Key, out var knownAddr))
                {
                    try
                    {
                        // Verify MaxHP still matches at known location
                        var verify = _explorer.Scanner.ReadBytes(knownAddr + HeapOffMaxHp, 2);
                        if (verify.Length == 2 && BitConverter.ToUInt16(verify, 0) == unit.MaxHp)
                        {
                            ReadPositionFromHeap(knownAddr, unit);
                            var hpBytes = _explorer.Scanner.ReadBytes(knownAddr + HeapOffHp, 2);
                            if (hpBytes.Length == 2) unit.Hp = BitConverter.ToUInt16(hpBytes, 0);
                            found++;
                            continue;
                        }
                    }
                    catch { }
                    _heapAddresses.Remove(kvp.Key);
                }

                // Search for MaxHP(u16) followed by MP(u16) — a 4-byte pattern that's
                // more unique than just MaxHP alone. At +0x0A: MaxHP(u16) MP(u16)
                byte maxHpLo = (byte)(unit.MaxHp & 0xFF);
                byte maxHpHi = (byte)(unit.MaxHp >> 8);
                byte mpLo = (byte)(unit.Mp & 0xFF);
                byte mpHi = (byte)(unit.Mp >> 8);
                var hpPattern = new byte[] { maxHpLo, maxHpHi, mpLo, mpHi };

                var matches = _explorer.SearchBytesInAllMemory(hpPattern, 30);

                // Collect all valid candidates, then pick the best one
                nint bestAddr = 0;
                int bestX = -1, bestY = -1, bestHp = 0;
                bool bestHasTurnFlag = false;

                foreach (var (addr, _) in matches)
                {
                    try
                    {
                        nint statStart = addr - HeapOffMaxHp;

                        // Verify level at +0x01
                        byte levelByte = _explorer.Scanner.ReadByte(statStart + 1);
                        if (levelByte != (byte)unit.Level) continue;

                        // Verify HP at +0x08 is reasonable
                        var hpCheck = _explorer.Scanner.ReadBytes(statStart + HeapOffHp, 2);
                        if (hpCheck.Length < 2) continue;
                        int hpVal = BitConverter.ToUInt16(hpCheck, 0);
                        if (hpVal <= 0 || hpVal > unit.MaxHp + 100) continue;

                        // Read position and turn flag
                        var posBytes = _explorer.ReadMultiple(new (nint, int)[]
                        {
                            (statStart + HeapOffX, 1),
                            (statStart + HeapOffY, 1),
                            (statStart + HeapOffTurnFlag, 1),
                        });
                        int x = (int)posBytes[0];
                        int y = (int)posBytes[1];
                        bool hasTurnFlag = posBytes[2] == 1;

                        if (x < 0 || x > 30 || y < 0 || y > 30) continue;

                        // Prefer: turnFlag=1 copy > non-zero position > any match
                        bool isBetter = false;
                        if (bestAddr == 0) isBetter = true;
                        else if (hasTurnFlag && !bestHasTurnFlag) isBetter = true;
                        else if (!bestHasTurnFlag && (x > 0 || y > 0) && bestX == 0 && bestY == 0) isBetter = true;

                        if (isBetter)
                        {
                            bestAddr = statStart;
                            bestX = x;
                            bestY = y;
                            bestHp = hpVal;
                            bestHasTurnFlag = hasTurnFlag;
                        }
                    }
                    catch { continue; }
                }

                if (bestAddr != 0)
                {
                    _heapAddresses[kvp.Key] = bestAddr;
                    unit.X = bestX;
                    unit.Y = bestY;
                    unit.Hp = bestHp;
                    found++;
                    ModLogger.LogDebug($"[BattleTracker] Found unit lv{unit.Level} t{unit.Team} at X={bestX} Y={bestY} flag={bestHasTurnFlag} (0x{bestAddr:X})");
                }

                if (sw.ElapsedMilliseconds > 5000)
                {
                    ModLogger.Log($"[BattleTracker] Heap scan timeout after {found} units, {sw.ElapsedMilliseconds}ms");
                    break;
                }

                // Only do one full search per scan cycle to avoid overloading
                if (found == 0 && sw.ElapsedMilliseconds > 2000)
                    break;
            }

            _heapScanDone = found > 0;
            if (found > 0)
                ModLogger.Log($"[BattleTracker] Heap scan: {found} units positioned in {sw.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// Fast refresh: re-read positions from already-known heap addresses.
        /// Invalidates cached addresses if data looks wrong.
        /// </summary>
        private void RefreshPositionsFromKnownAddresses()
        {
            var toRemove = new List<int>();
            foreach (var kvp in _heapAddresses)
            {
                if (!_units.TryGetValue(kvp.Key, out var unit)) continue;
                try
                {
                    // Verify MaxHP still matches (struct hasn't been reallocated)
                    var verify = _explorer.Scanner.ReadBytes(kvp.Value + HeapOffMaxHp, 2);
                    if (verify.Length < 2 || BitConverter.ToUInt16(verify, 0) != unit.MaxHp)
                    {
                        toRemove.Add(kvp.Key);
                        unit.X = -1;
                        unit.Y = -1;
                        continue;
                    }

                    ReadPositionFromHeap(kvp.Value, unit);
                    var hpBytes = _explorer.Scanner.ReadBytes(kvp.Value + HeapOffHp, 2);
                    if (hpBytes.Length == 2)
                        unit.Hp = BitConverter.ToUInt16(hpBytes, 0);
                }
                catch
                {
                    toRemove.Add(kvp.Key);
                    unit.X = -1;
                    unit.Y = -1;
                }
            }
            foreach (var key in toRemove)
                _heapAddresses.Remove(key);
        }

        private void ReadPositionFromHeap(nint statBase, BattleUnit unit)
        {
            try
            {
                var posReads = _explorer.ReadMultiple(new (nint, int)[]
                {
                    (statBase + HeapOffX, 1),
                    (statBase + HeapOffY, 1),
                });
                int x = (int)posReads[0];
                int y = (int)posReads[1];
                // Sanity check — grid coords should be 0-30
                if (x < 0 || x > 30 || y < 0 || y > 30) return;
                unit.StartX = x;
                unit.StartY = y;
                // Use start position as current if we don't have a live position yet
                if (unit.X < 0) unit.X = unit.StartX;
                if (unit.Y < 0) unit.Y = unit.StartY;
            }
            catch
            {
                // Silently ignore — heap address may be stale
            }
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

        [JsonPropertyName("startX")]
        public int StartX { get; set; }

        [JsonPropertyName("startY")]
        public int StartY { get; set; }

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
        // Populated from the roster's equipped slots. Lets Claude assess risk
        // ("this Knight has Counter Tackle") and plan accordingly.
        // NOT YET POPULATED — needs memory addresses for equipped R/S/M per unit.

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
        /// For point-target abilities (AoE=1, numeric HRange) AND radius-AoE
        /// abilities (AoE>1, numeric HRange), the set of tiles the caster can
        /// currently aim at. For point-target abilities the splash IS this tile;
        /// for radius abilities this is the CENTER list — see BestCenters for
        /// splash evaluation. Null for self-cast, line, cone, full-field, or
        /// non-numeric HRange. Populated only for the active player unit.
        /// </summary>
        [JsonPropertyName("validTargetTiles")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ValidTargetTile>? ValidTargetTiles { get; set; }

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
    }
}
