using System;
using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Locates and reads the out-of-battle "hovered unit array" — a heap-
    /// allocated 0x200-stride array that mirrors every active roster slot's
    /// current HP/MP/equipment. Discovered 2026-04-14 via per-unit brave+faith
    /// byte signatures: each struct stores `bb bb ff ff` at +0x2A (brave and
    /// faith each doubled) which, combined with roster-equipment bytes at
    /// +0x14..+0x27 (matches roster +0x08..+0x1B verbatim), uniquely
    /// identifies both the array base and which slot each struct belongs to.
    ///
    /// Why this struct matters: the roster array at 0x1411A18D0 only holds
    /// save-state equipment/brave/faith/level etc. The hovered-unit array
    /// ALSO contains computed runtime HP/MP — the numbers shown on the unit
    /// card that are NOT stored in the roster slot itself.
    ///
    /// Base address is heap, drifts across mod restarts — must rediscover
    /// on first use each session via AoB scan.
    ///
    /// Struct layout (0x200 bytes per unit, index 0 = roster slot 0 = Ramza):
    ///   +0x14..+0x27   Equipment block (7 u16 LE): helm/body/accessory/
    ///                  right-hand/left-hand/reserved/shield — same content
    ///                  as roster +0x08..+0x1B.
    ///   +0x28..+0x29   exp, level (u8 each)
    ///   +0x2A..+0x2D   brave brave faith faith (doubled u8)
    ///   +0x30..+0x33   HP / MaxHP (u16 LE each) — computed runtime value
    ///   +0x34..+0x37   MP / MaxMP (u16 LE each) — computed runtime value
    /// </summary>
    public class HoveredUnitArray
    {
        public const int SlotStride = 0x200;
        // The 7 u16 equipment slots start at +0x1A in the hovered struct,
        // mirroring roster +0x0E..+0x1B. The 6 bytes at struct +0x14..+0x19
        // are three non-equipment u16s (stat maxes or similar). The 20-byte
        // signature we AoB-scan for starts at +0x14 and covers both the 3
        // prefix u16s and the 7 equipment u16s.
        public const int SignatureOffset = 0x14;
        public const int EquipmentOffset = 0x1A;
        public const int BraveDoubledOffset = 0x2A;  // brave, brave
        public const int FaithDoubledOffset = 0x2C;  // faith, faith
        public const int HpOffset = 0x30;
        public const int MaxHpOffset = 0x32;
        public const int MpOffset = 0x34;
        public const int MaxMpOffset = 0x36;

        private readonly MemoryExplorer _explorer;
        private long _arrayBase = 0;
        private bool _discoveryAttempted = false;

        public HoveredUnitArray(MemoryExplorer explorer)
        {
            _explorer = explorer;
        }

        /// <summary>
        /// Current array base. Zero if not yet discovered. Use
        /// <see cref="Discover"/> (called implicitly by <see cref="ReadStats"/>)
        /// before treating this as valid.
        /// </summary>
        public long ArrayBase => _arrayBase;

        /// <summary>
        /// Invalidates the cached base so the next call will rediscover.
        /// Call this after a mod restart or if memory reads start returning
        /// obviously-wrong values.
        /// </summary>
        public void Invalidate()
        {
            _arrayBase = 0;
            _discoveryAttempted = false;
        }

        /// <summary>
        /// Discover the array base via AoB scan using Ramza's roster
        /// equipment bytes at +0x08..+0x1B as the signature. Only the match
        /// with immediately-preceding hovered-struct prefix bytes is
        /// accepted (filters out the half-dozen other heap copies of the
        /// roster). Returns true on success.
        /// </summary>
        public bool Discover()
        {
            if (_arrayBase != 0) return true;
            _discoveryAttempted = true;

            // Read Ramza's roster bytes 0x08..0x1B = 20 bytes, the 7-slot
            // equipment region. Every slot's equipment block is unique
            // enough to serve as an AoB anchor.
            const long RamzaRosterBase = 0x1411A18D0;
            var sigReads = new (nint, int)[20];
            for (int i = 0; i < 20; i++)
                sigReads[i] = ((nint)(RamzaRosterBase + 0x08 + i), 1);
            var raw = _explorer.ReadMultiple(sigReads);
            var sig = new byte[20];
            for (int i = 0; i < 20; i++) sig[i] = (byte)raw[i];

            var matches = _explorer.SearchBytesInAllMemory(sig, maxResults: 30);
            if (matches.Count == 0) return false;

            // Distinguish the hovered-array slot-0 entry from the half-dozen
            // other roster-mirror copies living in heap:
            //   - Roster backup copies have bytes +0x06..+0x07 = 0x90 0x06
            //     (roster's own first 8 bytes are literally "03 00 03 00
            //     90 00 90 06 ..." and the backups copy that verbatim).
            //   - The hovered-array struct has bytes +0x06..+0x07 = 0x90 0x03
            //     and its equipment block lives at +0x14 (not +0x08). This is
            //     the array we want.
            foreach (var (matchAddr, _) in matches)
            {
                long structBase = (long)matchAddr - SignatureOffset;
                var b6 = _explorer.ReadAbsolute((nint)(structBase + 6), 1);
                var b7 = _explorer.ReadAbsolute((nint)(structBase + 7), 1);
                if (b6 != null && b7 != null
                    && b6.Value.value == 0x90 && b7.Value.value == 0x03)
                {
                    _arrayBase = structBase;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Per-slot live stats read from the hovered-unit array.
        /// </summary>
        public class LiveStats
        {
            public int Hp, MaxHp;
            public int Mp, MaxMp;
            public int[] EquipmentU16 = new int[7]; // 7 u16 slots, 0xFF = empty
        }

        /// <summary>
        /// Reads live HP/MP/equipment at hovered-array index <paramref name="arrayIndex"/>,
        /// but only returns the data if the doubled brave/faith bytes at
        /// +0x2A..+0x2D match <paramref name="expectedBrave"/> and
        /// <paramref name="expectedFaith"/>. This guards against ghost data
        /// in array slots that haven't been populated with the unit the
        /// caller expects. Returns null when the sanity check fails so the
        /// caller can safely omit HP/equipment for that unit.
        /// </summary>
        public LiveStats? ReadStatsIfMatches(int arrayIndex, int expectedBrave, int expectedFaith)
        {
            if (!Discover()) return null;
            if (arrayIndex < 0 || arrayIndex >= 50) return null;

            long b = _arrayBase + (long)arrayIndex * SlotStride;

            // Sanity-check via the doubled brave/faith bytes at +0x2A..+0x2D
            // before trusting anything else at this array index.
            var sanity = _explorer.ReadMultiple(new (nint, int)[]
            {
                ((nint)(b + BraveDoubledOffset),     1),
                ((nint)(b + BraveDoubledOffset + 1), 1),
                ((nint)(b + FaithDoubledOffset),     1),
                ((nint)(b + FaithDoubledOffset + 1), 1),
            });
            if ((int)sanity[0] != expectedBrave || (int)sanity[1] != expectedBrave
             || (int)sanity[2] != expectedFaith || (int)sanity[3] != expectedFaith)
                return null;

            var reads = new (nint, int)[]
            {
                ((nint)(b + HpOffset), 2),
                ((nint)(b + MaxHpOffset), 2),
                ((nint)(b + MpOffset), 2),
                ((nint)(b + MaxMpOffset), 2),
                ((nint)(b + EquipmentOffset + 0),  2),
                ((nint)(b + EquipmentOffset + 2),  2),
                ((nint)(b + EquipmentOffset + 4),  2),
                ((nint)(b + EquipmentOffset + 6),  2),
                ((nint)(b + EquipmentOffset + 8),  2),
                ((nint)(b + EquipmentOffset + 10), 2),
                ((nint)(b + EquipmentOffset + 12), 2),
            };
            var v = _explorer.ReadMultiple(reads);
            var stats = new LiveStats
            {
                Hp = (int)v[0],
                MaxHp = (int)v[1],
                Mp = (int)v[2],
                MaxMp = (int)v[3],
            };
            for (int i = 0; i < 7; i++) stats.EquipmentU16[i] = (int)v[4 + i];
            return stats;
        }
    }
}
