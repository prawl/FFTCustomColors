using System;
using System.Collections.Generic;
using System.Text;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Reads character names from the master name table in game memory.
    ///
    /// The game stores a flat array of null-terminated ASCII strings starting with
    /// "Ramza\0Delita\0Argath\0Zalbaag\0..." and continuing through all story
    /// character names and then a large pool of generic recruit names (Wilham,
    /// Kenrick, Lloyd, etc). The table is indexed by integer — roster slot's
    /// +0x02 field holds the name table index for that slot's character.
    ///
    /// Pure parser (ParseNameTable) is static and testable. The instance wrapper
    /// handles the memory search + caching.
    /// </summary>
    public class NameTableLookup
    {
        private readonly MemoryExplorer _explorer;
        private Dictionary<int, string>? _nameCache;
        private long _tableBase = 0;
        private bool _buildAttempted = false;

        // 60-byte signature: first 9 story names from the master name table.
        //
        // We can't use a shorter anchor because there are multiple tables in memory
        // that share a "Ramza\0Delita\0Argath\0Zalbaag\0..." prefix:
        //   1. Per-roster-slot records at ~0x280 stride — each record lists 9 story
        //      name alternatives (Ramza through "Orland"), then binary stat data.
        //   2. The actual master name table — same opening but continues with
        //      "Orlandeau" (full name) at position 9 instead of "Orland".
        //
        // Using "Orlandeau\0" as the distinguishing suffix guarantees we hit the
        // master table, which has all ~500 character names we need to read.
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
            0x4F, 0x72, 0x6C, 0x61, 0x6E, 0x64, 0x65, 0x61, 0x75, 0x00 // "Orlandeau\0"
        };

        public NameTableLookup(MemoryExplorer explorer)
        {
            _explorer = explorer;
        }

        /// <summary>
        /// Returns the character name at the given table index, or null if not found.
        /// On first call, locates the table in memory and builds the full index map.
        ///
        /// The memory search is expensive (~900MB scan) so it runs at most once per
        /// session. After the first attempt, subsequent calls return from cache —
        /// even on failure, we remember the failure so we don't retry and stall
        /// every scan. Call Invalidate() to force a retry (e.g. after a game restart).
        /// </summary>
        public string? GetNameByIndex(int index)
        {
            if (index <= 0 || index > 2000) return null;

            if (!_buildAttempted)
            {
                _buildAttempted = true;
                TryBuildCache();
            }

            if (_nameCache == null) return null;
            return _nameCache.TryGetValue(index, out var name) ? name : null;
        }

        /// <summary>
        /// Forces a rebuild of the cache on next lookup. Call this between battles
        /// or after a restart in case the heap address shifted.
        /// </summary>
        public void Invalidate()
        {
            _nameCache = null;
            _tableBase = 0;
            _buildAttempted = false;
        }

        /// <summary>
        /// Pure parser: walks a byte buffer containing the name table and returns
        /// an index → name map. Index starts at 1 (Ramza). Stops when it encounters
        /// non-printable bytes or empty string runs, which indicate the end of the
        /// table.
        ///
        /// Rules:
        /// - Each name is a null-terminated ASCII string (0x20-0x7E range).
        /// - Max name length 31 characters — beyond that is garbage.
        /// - Index starts at 1 (1-based, matches roster slot +0x02 values).
        /// - Table ends at first non-printable byte inside a pending string.
        /// - Consecutive nulls (empty strings) are tolerated up to a limit, then
        ///   terminate parsing.
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
                        // Valid character range: printable ASCII (0x20-0x7E) plus
                        // extended high bytes (0x80-0xFF) to allow UTF-8 sequences for
                        // accented names like "Cúchulainn". Control bytes (0x01-0x1F)
                        // and DEL (0x7F) indicate non-string data.
                        bool valid = true;
                        for (int k = strStart; k < i; k++)
                        {
                            byte bk = bytes[k];
                            if (bk < 0x20 || bk == 0x7F) { valid = false; break; }
                        }
                        if (valid)
                        {
                            // Decode as UTF-8 so accented chars survive. Falls back to
                            // empty string on invalid sequences, which we just skip.
                            var name = Encoding.UTF8.GetString(bytes, strStart, len);
                            cache[index++] = name;
                            consecutiveNulls = 0;
                        }
                        else
                        {
                            // Hit non-string data — table has ended.
                            break;
                        }
                    }
                    else if (len == 0)
                    {
                        // Empty string (consecutive nulls). A few are tolerated but
                        // a run of them means we're past the table's last entry.
                        consecutiveNulls++;
                        if (consecutiveNulls > 3 && cache.Count > 10) break;
                    }
                    else
                    {
                        // Over-long string: not valid table data.
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
                // master name table lives in heap-allocated UE4 data, not read-only
                // sections. SearchBytesAllRegions is wider (covers PAGE_READONLY etc.)
                // but crashes the game on large scans due to unsafe reads in some
                // regions. The narrower search is safe and has been verified to find
                // the table (confirmed via bridge `search_bytes` command reaching
                // 0x4E17FBDFD1 during investigation).
                var matches = _explorer.SearchBytesInAllMemory(AnchorPattern, maxResults: 8);
                if (matches == null || matches.Count == 0)
                {
                    ModLogger.Log("[NameTableLookup] No anchor match found");
                    return false;
                }

                ModLogger.Log($"[NameTableLookup] {matches.Count} anchor matches found");

                // Try each match until one parses cleanly. Some matches may be in
                // read-only memory segments that can't be fully read, or may be
                // truncated copies — iterate to find one that actually works.
                Dictionary<int, string>? best = null;
                long bestBase = 0;
                foreach (var (matchAddr, _) in matches)
                {
                    var bytes = _explorer.Scanner.ReadBytes((nint)matchAddr, 8192);
                    if (bytes == null || bytes.Length < 128)
                    {
                        ModLogger.Log($"[NameTableLookup]   0x{(long)matchAddr:X}: read failed or too short ({bytes?.Length ?? 0} bytes)");
                        continue;
                    }

                    var cache = ParseNameTable(bytes);
                    ModLogger.Log($"[NameTableLookup]   0x{(long)matchAddr:X}: parsed {cache.Count} names");

                    if (cache.Count > (best?.Count ?? 0))
                    {
                        best = cache;
                        bestBase = (long)matchAddr;
                    }
                }

                if (best == null || best.Count == 0)
                {
                    ModLogger.Log("[NameTableLookup] All match attempts produced 0 names");
                    return false;
                }

                _nameCache = best;
                _tableBase = bestBase;
                ModLogger.Log($"[NameTableLookup] Selected 0x{_tableBase:X} with {_nameCache.Count} names (Ramza={_nameCache.GetValueOrDefault(1)}, Wilham={_nameCache.GetValueOrDefault(76)}, Kenrick={_nameCache.GetValueOrDefault(103)})");
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
