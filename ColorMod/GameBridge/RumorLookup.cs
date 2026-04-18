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
    }
}
