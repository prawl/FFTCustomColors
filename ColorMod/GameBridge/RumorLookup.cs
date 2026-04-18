using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Lookup for the 26 brave-story rumor bodies decoded from world_wldmes_bin.en.bin.
    /// Corpus is hardcoded in RumorCorpus.cs (generated once from the pac file) to avoid
    /// shipping a 4MB binary with the mod.
    /// </summary>
    public class RumorLookup
    {
        private readonly List<WorldMesDecoder.Rumor> _rumors = new();

        /// <summary>
        /// Hardcoded map of known Tavern rumor titles → corpus body indices. Populated
        /// empirically from in-game screenshots — titles are UI labels that do NOT appear
        /// in any data file we can read, so the mapping is maintained by hand as new
        /// titles are observed. Case-insensitive key lookup.
        /// </summary>
        private static readonly Dictionary<string, int> TitleToIndex =
            new(System.StringComparer.OrdinalIgnoreCase)
        {
            { "The Legend of the Zodiac Braves", 10 },
            { "Zodiac Stones", 11 },
            { "The Horror of Riovanes", 19 },
            // TODO: populate as each tavern's rumors are observed live.
            // Known-unmapped: "At Bael's End" (Dorter chapter-4 row 3) — body is NOT in
            // world_wldmes.bin; lives in a different resource file.
        };

        public RumorLookup()
        {
            for (int i = 0; i < RumorCorpus.Bodies.Length; i++)
                _rumors.Add(new WorldMesDecoder.Rumor(i, 0, RumorCorpus.Bodies[i]));
        }

        public int Count => _rumors.Count;
        public string? SourcePath => "hardcoded:RumorCorpus";

        public WorldMesDecoder.Rumor? GetByIndex(int index)
        {
            if (index < 0 || index >= _rumors.Count) return null;
            return _rumors[index];
        }

        public IReadOnlyList<WorldMesDecoder.Rumor> All => _rumors;

        public WorldMesDecoder.Rumor? GetByBodySubstring(string needle)
        {
            if (string.IsNullOrWhiteSpace(needle)) return null;
            foreach (var r in _rumors)
            {
                if (r.Body.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return r;
            }
            return null;
        }

        /// <summary>
        /// Look up a rumor body by its UI title (e.g. "The Legend of the Zodiac Braves").
        /// Returns null if the title is not in the hardcoded map.
        /// </summary>
        public WorldMesDecoder.Rumor? GetByTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;
            if (!TitleToIndex.TryGetValue(title.Trim(), out int idx)) return null;
            return GetByIndex(idx);
        }

        /// <summary>
        /// All known title → index mappings. Used by tests and diagnostic tooling.
        /// </summary>
        public static IReadOnlyDictionary<string, int> KnownTitles => TitleToIndex;
    }
}
