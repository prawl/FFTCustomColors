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

        // Chapter-1 uniform rumor row layout. Every Chapter-1 settlement
        // visited in sessions 33-40 (Dorter/Gariland/Warjilis/Yardrow/Goug/
        // Zaland) shows these three rumors in this order. Row 3 is always
        // "At Bael's End" which has no matching corpus entry (different
        // source file — see TavernRumorTitleMap.md). When a Chapter-2+
        // settlement diverges, split this into per-chapter tables.
        //
        // Adding a newly-visited Chapter-1 city is one line: reference this
        // dictionary from the Table entry.
        internal static readonly IReadOnlyDictionary<int, int> Chapter1UniformRows =
            new Dictionary<int, int>
            {
                { 0, 10 },  // "The Legend of the Zodiac Braves"
                { 1, 11 },  // "Zodiac Stones"
                { 2, 19 },  // "The Horror of Riovanes"
                // row 3 unmapped — "At Bael's End" body not in world_wldmes.bin
            };

        // cityId → (row → corpusIndex). Rows that map to null / missing entries
        // return null from Lookup.
        private static readonly Dictionary<int, IReadOnlyDictionary<int, int>> Table =
            new()
            {
                // Sessions 33-40 at each tavern confirmed identical rumor set
                // during Chapter 1. As Chapter-2+ gets explored and divergences
                // appear, split cities out of this uniform block into per-city
                // dictionaries as needed.
                [CityId.Dorter]   = Chapter1UniformRows,  // session 33
                [CityId.Gariland] = Chapter1UniformRows,  // session 36
                [CityId.Warjilis] = Chapter1UniformRows,  // session 37
                [CityId.Yardrow]  = Chapter1UniformRows,  // session 38
                [CityId.Goug]     = Chapter1UniformRows,  // session 39
                [CityId.Zaland]   = Chapter1UniformRows,  // session 40
                [CityId.Lesalia]  = Chapter1UniformRows,  // session 41 (capital)
                [CityId.Bervenia] = Chapter1UniformRows,  // session 43 (trade hub)

                // Gollund (session 42) — FIRST divergence from the Chapter-1
                // uniform set. Row 3 is a Gollund-specific rumor "The Haunted
                // Mine" (corpus #20, "Monsters have taken up residence in one
                // of the many coal mines in Gollund..."). Row 4 is the usual
                // "At Bael's End" (unmapped). This breaks the Chapter-1 uniform
                // hypothesis — future cities may also have local-flavor rumors
                // slotted in at row 3 or similar.
                [CityId.Gollund] = new Dictionary<int, int>
                {
                    { 0, 10 }, // Zodiac Braves
                    { 1, 11 }, // Zodiac Stones
                    { 2, 19 }, // Horror of Riovanes
                    { 3, 20 }, // The Haunted Mine (Gollund-specific)
                    // row 4: At Bael's End — unmapped
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
        /// Returns true when the given city is seeded with the shared
        /// Chapter1UniformRows dictionary — i.e. its rumor list matches the
        /// hypothesis that Chapter-1 settlements show an identical rumor set.
        /// False for non-settlement IDs, unseeded cities, or any city that
        /// eventually gets split out to a per-city dictionary.
        /// </summary>
        public static bool IsChapter1UniformCity(int cityId)
        {
            return Table.TryGetValue(cityId, out var rows)
                && ReferenceEquals(rows, Chapter1UniformRows);
        }

        /// <summary>
        /// Read-only snapshot of the full (cityId → row → corpusIndex) table.
        /// Useful for diagnostic callers that want to iterate "for each
        /// seeded city, show its full rumor row list" without repeated
        /// Lookup probes. Returned dictionaries are the live internal
        /// references; do not mutate.
        /// </summary>
        public static System.Collections.Generic.IReadOnlyDictionary<int, System.Collections.Generic.IReadOnlyDictionary<int, int>> TableSnapshot => Table;

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
