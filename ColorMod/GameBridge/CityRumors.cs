using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Maps (cityId, row) → RumorCorpus index for the rumors shown on each city's
    /// Tavern Rumors list. Built up empirically by visiting each settlement and
    /// matching displayed titles to the hardcoded corpus.
    ///
    /// The table is intentionally sparse — not every city has every row mapped
    /// yet, and some rumor titles (e.g. "At Bael's End" at Dorter row 3) are NOT
    /// in the world_wldmes.bin corpus at all. Those rows return null until the
    /// alternative rumor source file is decoded.
    /// </summary>
    public static class CityRumors
    {
        /// <summary>
        /// Named location IDs for the 15 settlement cities. These match the
        /// world-map travel IDs used by world_travel_to and the settlement
        /// list in FFTHandsFree/Instructions/Shopping.md. Kept as consts
        /// (not an enum) so the raw-int APIs stay the primary contract.
        /// </summary>
        public static class CityId
        {
            public const int Lesalia = 0;
            public const int Riovanes = 1;
            public const int Eagrose = 2;
            public const int Lionel = 3;
            public const int Limberry = 4;
            public const int Zeltennia = 5;
            public const int Gariland = 6;
            public const int Yardrow = 7;
            public const int Gollund = 8;
            public const int Dorter = 9;
            public const int Zaland = 10;
            public const int Goug = 11;
            public const int Warjilis = 12;
            public const int Bervenia = 13;
            public const int SalGhidos = 14;

            private static readonly string[] NamesByIdArray =
            {
                "Lesalia", "Riovanes", "Eagrose", "Lionel", "Limberry",
                "Zeltennia", "Gariland", "Yardrow", "Gollund", "Dorter",
                "Zaland", "Goug", "Warjilis", "Bervenia", "SalGhidos",
            };

            /// <summary>
            /// Returns the canonical settlement name for the given city id, or
            /// null if the id is out of range. Used for friendly error messages
            /// and diagnostic dumps.
            /// </summary>
            public static string? NameFor(int id)
            {
                if (id < 0 || id >= NamesByIdArray.Length) return null;
                return NamesByIdArray[id];
            }

            /// <summary>
            /// Returns the settlement id for the given name (case-insensitive),
            /// or -1 if the name is not a canonical settlement. -1 matches the
            /// CommandRequest.LocationId "unset" sentinel so the result can be
            /// fed directly into get_rumor.
            /// </summary>
            public static int IdFor(string? name)
            {
                if (string.IsNullOrEmpty(name)) return -1;
                for (int i = 0; i < NamesByIdArray.Length; i++)
                {
                    if (string.Equals(NamesByIdArray[i], name, System.StringComparison.OrdinalIgnoreCase))
                        return i;
                }
                return -1;
            }
        }

        // cityId → (row → corpusIndex). Rows that map to null / missing entries
        // return null from Lookup.
        private static readonly Dictionary<int, Dictionary<int, int>> Table =
            new()
            {
                // Dorter, verified live session 33 at Dorter Tavern:
                //   row 0 → "The Legend of the Zodiac Braves" (corpus #10)
                //   row 1 → "Zodiac Stones"                   (corpus #11)
                //   row 2 → "The Horror of Riovanes"          (corpus #19)
                //   row 3 → "At Bael's End"                    NOT in corpus — unmapped.
                [CityId.Dorter] = new Dictionary<int, int>
                {
                    { 0, 10 },
                    { 1, 11 },
                    { 2, 19 },
                },
                // Gariland, verified live session 36 at Gariland Tavern
                // during Chapter 1 story state. Identical rumor set to
                // Dorter for Chapter 1 — same 4 titles in same order. If the
                // Chapter-2+ rumor set diverges, split into a per-chapter table.
                [CityId.Gariland] = new Dictionary<int, int>
                {
                    { 0, 10 },
                    { 1, 11 },
                    { 2, 19 },
                },
                // Warjilis, verified live session 37 at Warjilis Tavern during
                // Chapter 1. Same 4 titles as Dorter/Gariland — the Chapter-1
                // rumor list is shared across all visited cities so far. When
                // a city finally diverges (likely a post-Chapter-2 visit), the
                // ChapterXCityRumors split will become necessary.
                [CityId.Warjilis] = new Dictionary<int, int>
                {
                    { 0, 10 },
                    { 1, 11 },
                    { 2, 19 },
                },
                // Yardrow, verified live session 38 at Yardrow Tavern during
                // Chapter 1. Fourth city confirming the Chapter-1 uniform-list
                // hypothesis. Still same 4 titles.
                [CityId.Yardrow] = new Dictionary<int, int>
                {
                    { 0, 10 },
                    { 1, 11 },
                    { 2, 19 },
                },
            };

        /// <summary>
        /// Returns the RumorCorpus index for (cityId, row), or null if the pair
        /// is not in the table. Callers should fall back to title/substring
        /// resolution when this returns null.
        /// </summary>
        public static int? Lookup(int cityId, int row)
        {
            if (!Table.TryGetValue(cityId, out var cityRows)) return null;
            if (!cityRows.TryGetValue(row, out int idx)) return null;
            return idx;
        }

        /// <summary>
        /// Reverse lookup: given a corpus index, return every (cityId, row) pair
        /// that maps to it. Useful for surfacing which city's rumor list contains
        /// a given body when expanding the title map.
        /// </summary>
        public static System.Collections.Generic.IReadOnlyList<(int CityId, int Row)> CitiesFor(int corpusIndex)
        {
            var hits = new System.Collections.Generic.List<(int, int)>();
            foreach (var cityEntry in Table)
            {
                foreach (var rowEntry in cityEntry.Value)
                {
                    if (rowEntry.Value == corpusIndex)
                        hits.Add((cityEntry.Key, rowEntry.Key));
                }
            }
            return hits;
        }

        /// <summary>
        /// Flat enumeration of every (cityId, row, corpusIndex) triple in the
        /// table. Used by the test suite to assert invariants across all
        /// seeded mappings — any new seed entry is automatically covered by
        /// the range/validity guards.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<(int CityId, int Row, int CorpusIndex)> AllMappings
        {
            get
            {
                foreach (var cityEntry in Table)
                    foreach (var rowEntry in cityEntry.Value)
                        yield return (cityEntry.Key, rowEntry.Key, rowEntry.Value);
            }
        }
    }
}
