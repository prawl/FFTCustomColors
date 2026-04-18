using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Reads character names from the per-roster-slot name records in game memory.
    ///
    /// The game keeps a table of roster slot records, one per unit (Ramza + generic
    /// recruits + story characters), each 0x280 bytes wide. At offset +0x10 inside
    /// each record is a list of null-terminated UTF-8 name alternatives starting
    /// with the CHOSEN (displayed) name. For slot 0 (Ramza) the list is the 9 main
    /// story characters — Ramza always appears first. For generic recruits, the
    /// first string is the recruit's chosen name.
    ///
    /// HOW TO USE:
    /// Call GetNameBySlot(N) where N is the roster slot index (0 = Ramza, 1 = first
    /// recruit, etc). Returns the chosen display name, or null if the slot is out
    /// of range or the table hasn't been located yet.
    ///
    /// PERFORMANCE:
    /// On the first call, searches memory once for the table anchor and reads
    /// ~16KB of record data. Subsequent calls are O(1) dictionary lookups. The
    /// cache persists for the mod process lifetime — call Invalidate() to refresh
    /// after a heap relocation (rarely needed).
    ///
    /// WHY THIS EXISTS:
    /// UnitNameLookup has hardcoded story character names. For generic player
    /// recruits (like a random-named Knight), we need to read the actual name
    /// from game memory. The roster slot at 0x1411A18D0 does NOT contain the
    /// name string directly — names live in this separate per-slot record table.
    /// </summary>
    public class NameTableLookup
    {
        private readonly MemoryExplorer _explorer;
        private Dictionary<int, string>? _slotNameCache;
        private long _tableBase = 0;
        private bool _buildAttempted = false;

        /// <summary>
        /// Stride between roster slot records in the name table.
        /// </summary>
        public const int RecordStride = 0x280;

        /// <summary>
        /// Offset of the first (chosen) character name inside each record, relative
        /// to the record's start.
        /// </summary>
        public const int NameOffsetInRecord = 0x10;

        /// <summary>
        /// Max characters to read for a single name — longer than this and it's
        /// not a valid name, so treat as table end.
        /// </summary>
        public const int MaxNameLength = 31;

        // 57-byte signature: the ROSTER slot table's opening — 9 name alternatives
        // for slot 0 (Ramza). Each slot record is 0x280 bytes, name alternatives
        // start at +0x10 in each record, and slot 0's alternatives always end with
        // "Orland\0" (6 chars, truncated — NOT "Orlandeau\0" which appears in the
        // separate master name table).
        //
        // Using "Orland\0" as the final suffix distinguishes the per-slot roster
        // table from the flat master name pool. The roster table is what we want:
        // it's ordered by roster slot, stride 0x280, and the FIRST string in each
        // record is the character's displayed name.
        //
        // Verified empirically: searching for this signature finds a base like
        // 0x41667B2034. At that base:
        //   +0x0010: "Ramza"   (slot 0)
        //   +0x0290: "Kenrick" (slot 1)
        //   +0x0510: "Lloyd"   (slot 2)
        //   +0x0790: "Wilham"  (slot 3)
        //   +0x0A10: "Alicia"  (slot 4)
        private static readonly byte[] AnchorPattern =
        {
            0x52, 0x61, 0x6D, 0x7A, 0x61, 0x00, // "Ramza\0"
            0x44, 0x65, 0x6C, 0x69, 0x74, 0x61, 0x00, // "Delita\0"
            0x41, 0x72, 0x67, 0x61, 0x74, 0x68, 0x00, // "Argath\0"
            0x5A, 0x61, 0x6C, 0x62, 0x61, 0x61, 0x67, 0x00, // "Zalbaag\0"
            0x44, 0x79, 0x63, 0x65, 0x64, 0x61, 0x72, 0x67, 0x00, // "Dycedarg\0"
            0x4C, 0x61, 0x72, 0x67, 0x00, // "Larg\0"
            0x47, 0x6F, 0x6C, 0x74, 0x61, 0x6E, 0x6E, 0x61, 0x00, // "Goltanna\0"
            0x4F, 0x76, 0x65, 0x6C, 0x69, 0x61, 0x00, // "Ovelia\0"
            0x4F, 0x72, 0x6C, 0x61, 0x6E, 0x64, 0x00 // "Orland\0" (roster-table truncation)
        };

        public NameTableLookup(MemoryExplorer explorer)
        {
            _explorer = explorer;
        }

        /// <summary>
        /// Returns the displayed character name for the given roster slot (0-based),
        /// or null if the slot is out of range or the name table can't be located.
        ///
        /// On first call, searches memory for the table anchor. On failure the
        /// attempt is remembered so subsequent calls return null immediately without
        /// retrying the expensive search. Call Invalidate() to force a retry.
        /// </summary>
        public string? GetNameBySlot(int slot)
        {
            if (slot < 0 || slot >= 30) return null;

            if (!_buildAttempted)
            {
                _buildAttempted = true;
                TryBuildCache();
            }

            if (_slotNameCache == null) return null;
            return _slotNameCache.TryGetValue(slot, out var name) ? name : null;
        }

        /// <summary>
        /// Forces a rebuild of the cache on next lookup. Call this after a heap
        /// relocation or if the game state changes significantly.
        /// </summary>
        public void Invalidate()
        {
            _slotNameCache = null;
            _tableBase = 0;
            _buildAttempted = false;
        }

        /// <summary>
        /// Pure parser: walks a byte buffer as a stride-based roster name table.
        /// Each record is RecordStride bytes; the first null-terminated UTF-8 name
        /// at +NameOffsetInRecord inside each record is that slot's displayed name.
        /// Returns slot → name map, where slot 0 is the first record in the buffer.
        ///
        /// Rules:
        /// - Names must start with a printable character (0x20-0x7E or UTF-8 high byte).
        /// - Names must be <= MaxNameLength bytes before the null terminator.
        /// - A record with no valid name (zero-length or garbage at +0x10) terminates parsing.
        ///   This is how we know we've walked past the end of the recruit list.
        /// </summary>
        public static Dictionary<int, string> ParseRosterNameTable(byte[] bytes)
        {
            var cache = new Dictionary<int, string>();
            if (bytes == null || bytes.Length < NameOffsetInRecord + 1) return cache;

            int maxSlots = bytes.Length / RecordStride;
            for (int slot = 0; slot < maxSlots; slot++)
            {
                int nameStart = slot * RecordStride + NameOffsetInRecord;
                if (nameStart + 1 >= bytes.Length) break;

                // Read up to MaxNameLength bytes or until null terminator
                int nameEnd = nameStart;
                while (nameEnd < bytes.Length && nameEnd - nameStart < MaxNameLength)
                {
                    if (bytes[nameEnd] == 0) break;
                    nameEnd++;
                }

                int len = nameEnd - nameStart;
                if (len == 0)
                {
                    // Empty name — this slot has no occupant, we've walked past
                    // the end of the recruit list.
                    break;
                }
                if (len >= MaxNameLength)
                {
                    // No null terminator found within max length — not valid.
                    break;
                }

                // Validate characters: must be printable ASCII or UTF-8 high bytes.
                bool valid = true;
                for (int k = nameStart; k < nameEnd; k++)
                {
                    byte bk = bytes[k];
                    if (bk < 0x20 || bk == 0x7F) { valid = false; break; }
                }
                if (!valid) break;

                var name = Encoding.UTF8.GetString(bytes, nameStart, len);
                cache[slot] = name;
            }

            return cache;
        }

        /// <summary>
        /// Pure candidate-selection: given a list of (recordBase, parsed-cache)
        /// pairs from AoB match probes, return the canonical roster base or
        /// null if no candidate looks right.
        ///
        /// Rule: take the LOWEST-ADDRESS candidate whose slot 0 == "Ramza" AND
        /// whose slot count equals the maximum observed across all candidates.
        /// Low-address preference pins us to the stable main-module / static roster
        /// region, avoiding ghost heap allocations that happen to begin with the
        /// same 9 story names but diverge on later slots (the session-27
        /// "Crestian → Reis" bug).
        /// </summary>
        public static (long recordBase, Dictionary<int, string> cache)? SelectBestRosterBase(
            IEnumerable<(long recordBase, Dictionary<int, string> cache)> candidates)
        {
            if (candidates == null) return null;
            var list = candidates.ToList();
            if (list.Count == 0) return null;

            int maxCount = 0;
            foreach (var c in list)
            {
                if (c.cache != null && c.cache.Count > maxCount) maxCount = c.cache.Count;
            }
            if (maxCount == 0) return null;

            // Order ascending by address, take first that satisfies both
            // "slot 0 == Ramza" and "count == max". Use unsigned compare so
            // negative-looking long values (rare, but heap addresses can run high)
            // sort intuitively.
            foreach (var c in list.OrderBy(c => (ulong)c.recordBase))
            {
                if (c.cache == null) continue;
                if (!c.cache.TryGetValue(0, out var slot0) || slot0 != "Ramza") continue;
                if (c.cache.Count != maxCount) continue;
                return c;
            }
            return null;
        }

        /// <summary>
        /// LEGACY: pure flat-table parser kept for backward compatibility with existing tests.
        /// This was used when we thought the name table was a flat list indexed by nameId.
        /// The real structure is per-slot records (see ParseRosterNameTable). Not used
        /// at runtime anymore.
        /// </summary>
        public static Dictionary<int, string> ParseNameTable(byte[] bytes)
        {
            var cache = new Dictionary<int, string>();
            if (bytes == null || bytes.Length == 0) return cache;

            int index = 1;
            int strStart = 0;
            int consecutiveNulls = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (b == 0)
                {
                    int len = i - strStart;
                    if (len > 0 && len < 32)
                    {
                        bool valid = true;
                        for (int k = strStart; k < i; k++)
                        {
                            byte bk = bytes[k];
                            if (bk < 0x20 || bk == 0x7F) { valid = false; break; }
                        }
                        if (valid)
                        {
                            var name = Encoding.UTF8.GetString(bytes, strStart, len);
                            cache[index++] = name;
                            consecutiveNulls = 0;
                        }
                        else
                        {
                            break;
                        }
                    }
                    else if (len == 0)
                    {
                        consecutiveNulls++;
                        if (consecutiveNulls > 3 && cache.Count > 10) break;
                    }
                    else
                    {
                        break;
                    }
                    strStart = i + 1;
                }
            }

            return cache;
        }

        private bool TryBuildCache()
        {
            try
            {
                // Use SearchBytesInAllMemory (PAGE_READWRITE private/mapped only) — the
                // roster name table lives in heap-allocated UE4 data. SearchBytesAllRegions
                // is wider but crashes the game on large scans.
                var matches = _explorer.SearchBytesInAllMemory(AnchorPattern, maxResults: 8);
                if (matches == null || matches.Count == 0)
                {
                    ModLogger.Log("[NameTableLookup] No anchor match found");
                    return false;
                }

                ModLogger.Log($"[NameTableLookup] {matches.Count} anchor matches found");

                // The anchor pattern starts at "Ramza\0" which is at +0x10 inside the
                // first record. So record 0 starts at matchAddr - 0x10.
                //
                // Candidate selection: iterate matches in address order and take the
                // FIRST match whose parsed cache has slot 0 == "Ramza" AND parses
                // at least as many slots as the most-populous match. This prefers
                // stable low-address matches (the canonical roster) over high-heap
                // duplicates (battle scratch / dropped UI widgets that happen to
                // begin with the same 9 story names but diverge in later slots).
                //
                // Historical footgun (session 27 "Crestian → Reis" bug): picking by
                // "highest slot count wins" could select a stale heap allocation
                // that contains a prior save's roster data, mis-labeling live
                // recruits with names from the ghost table.
                var candidates = new List<(long recordBase, Dictionary<int, string> cache)>();
                foreach (var (matchAddr, _) in matches)
                {
                    long recordBase = (long)matchAddr - NameOffsetInRecord;
                    var bytes = _explorer.Scanner.ReadBytes((nint)recordBase, 16384);
                    if (bytes == null || bytes.Length < 1024)
                    {
                        ModLogger.Log($"[NameTableLookup]   0x{recordBase:X}: read failed or too short ({bytes?.Length ?? 0} bytes)");
                        continue;
                    }
                    var cache = ParseRosterNameTable(bytes);
                    ModLogger.Log($"[NameTableLookup]   0x{recordBase:X}: parsed {cache.Count} slots");
                    candidates.Add((recordBase, cache));
                }
                var selected = SelectBestRosterBase(candidates);
                Dictionary<int, string>? best = selected?.cache;
                long bestBase = selected?.recordBase ?? 0;

                if (best == null || best.Count == 0)
                {
                    ModLogger.Log("[NameTableLookup] All match attempts produced 0 slots");
                    return false;
                }

                _slotNameCache = best;
                _tableBase = bestBase;
                var sample = new List<string>();
                for (int i = 0; i < 5 && i < _slotNameCache.Count; i++)
                    sample.Add($"slot{i}={_slotNameCache.GetValueOrDefault(i) ?? "?"}");
                ModLogger.Log($"[NameTableLookup] Selected 0x{_tableBase:X} with {_slotNameCache.Count} slots ({string.Join(", ", sample)})");
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Log($"[NameTableLookup] Build failed: {ex.Message}");
                return false;
            }
        }
    }
}
