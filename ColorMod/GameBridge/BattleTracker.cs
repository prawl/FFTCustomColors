using System;
using System.Collections.Generic;
using System.Diagnostics;
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

            // Heap scanning disabled — it was causing game crashes.
            // Unit positions come from the condensed turn queue (active unit)
            // and will be re-enabled with a safer approach later.

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
                    MenuCursor = menuCursor,
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

            foreach (var kvp in _units)
            {
                var u = kvp.Value;
                state.Units.Add(new BattleUnitState
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
                });
            }

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
            var posReads = _explorer.ReadMultiple(new (nint, int)[]
            {
                (statBase + HeapOffX, 1),
                (statBase + HeapOffY, 1),
            });
            unit.StartX = (int)posReads[0];
            unit.StartY = (int)posReads[1];
            // Use start position as current if we don't have a live position yet
            if (unit.X < 0) unit.X = unit.StartX;
            if (unit.Y < 0) unit.Y = unit.StartY;
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

                        if (lvl >= 1 && lvl <= 99 && (team == 0 || team == 1))
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

        [JsonPropertyName("activeUnit")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ActiveUnitState? ActiveUnit { get; set; }

        [JsonPropertyName("units")]
        public List<BattleUnitState> Units { get; set; } = new();
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

        [JsonPropertyName("menuCursor")]
        public int MenuCursor { get; set; }
    }

    public class BattleUnitState
    {
        [JsonPropertyName("team")]
        public int Team { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }

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
    }
}
