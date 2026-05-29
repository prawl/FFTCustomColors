using System.Collections.Generic;
using System.Drawing;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Curated chocobo color presets, per tier. Each preset is a base color applied to the
    /// chocobo body (Primary section) via uniformHue shading. The three tiers share one sprite
    /// (battle_cyoko_spr.bin) as palettes 0/1/2 — see docs/CHOCOBO_COLOR_RESEARCH.md.
    /// </summary>
    public static class ChocoboThemePresets
    {
        public const string Original = "original";

        // Key under which the theme editor saves chocobo user themes (one shared, tier-agnostic
        // entry — a chocobo recolor works on any tier's palette). See AddMonsterRow / coordinator.
        public const string EditorKey = "Chocobo";

        // Config property key per tier, in display order.
        public static readonly string[] TierKeys = { "Chocobo_RankI", "Chocobo_RankII", "Chocobo_RankIII" };

        // tierKey -> (preset name -> base color). No duplicate names across tiers.
        private static readonly Dictionary<string, Dictionary<string, Color>> Presets =
            new Dictionary<string, Dictionary<string, Color>>
            {
                ["Chocobo_RankI"] = new Dictionary<string, Color>   // Yellow tier (palette 0)
                {
                    ["White"]  = Color.FromArgb(235, 235, 235),
                    ["Blue"]   = Color.FromArgb(45, 110, 225),
                    ["Orange"] = Color.FromArgb(240, 140, 30),
                },
                ["Chocobo_RankII"] = new Dictionary<string, Color>  // Black tier (palette 1)
                {
                    ["Crimson"] = Color.FromArgb(200, 30, 55),
                    ["Emerald"] = Color.FromArgb(30, 175, 95),
                    ["Violet"]  = Color.FromArgb(150, 70, 210),
                },
                ["Chocobo_RankIII"] = new Dictionary<string, Color> // Red tier (palette 2)
                {
                    ["Cyan"]    = Color.FromArgb(40, 195, 205),
                    ["Lime"]    = Color.FromArgb(140, 205, 45),
                    ["Magenta"] = Color.FromArgb(215, 55, 195),
                },
            };

        /// <summary>Palette index inside battle_cyoko_spr.bin for a tier (RankI=0, RankII=1, RankIII=2).</summary>
        public static int PaletteIndexForTier(string tierKey)
        {
            switch (tierKey)
            {
                case "Chocobo_RankI": return 0;
                case "Chocobo_RankII": return 1;
                case "Chocobo_RankIII": return 2;
                default: return 0;
            }
        }

        /// <summary>Theme names available for a tier's dropdown: "original" plus the tier's presets.</summary>
        public static List<string> GetThemeNames(string tierKey)
        {
            var names = new List<string> { Original };
            if (Presets.TryGetValue(tierKey, out var m))
                names.AddRange(m.Keys);
            return names;
        }

        /// <summary>Resolves a tier preset name to its base color. Returns false for "original"/unknown.</summary>
        public static bool TryGetBaseColor(string tierKey, string themeName, out Color color)
        {
            color = Color.Empty;
            return Presets.TryGetValue(tierKey, out var m) && m.TryGetValue(themeName, out color);
        }
    }
}
