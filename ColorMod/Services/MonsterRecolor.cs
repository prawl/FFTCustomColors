using System.Collections.Generic;
using System.Drawing;
using FFTColorCustomizer.ThemeEditor;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Pure palette recolor for a monster bin. Recolors one palette's section indices to a base
    /// color, preserving the section's shade relationships (RelativeShadeGenerator). Mutates the
    /// byte[] in place. Each palette is 16 colors * 2 bytes (16-bit BGR555) at offset
    /// paletteIndex*32; index 0 is transparent and never touched. Generic across all families —
    /// the family/tier wiring lives in <see cref="MonsterThemeRegistry"/>.
    /// </summary>
    public static class MonsterRecolor
    {
        /// <summary>
        /// Applies a monster theme (built-in preset or user-saved theme) to one tier's palette in
        /// binData. Presets recolor via uniformHue from a base color; user themes copy the saved
        /// palette's section colors. Returns true if anything was applied ("original"/unknown = false).
        /// </summary>
        public static bool ApplyTheme(byte[] binData, int paletteIndex, IEnumerable<JobSection> sections,
            string tierKey, string themeName, UserThemeService userThemes)
        {
            if (string.IsNullOrEmpty(themeName) || themeName == MonsterThemeRegistry.Original)
                return false;

            if (MonsterThemeRegistry.TryGetPreset(tierKey, themeName, out var preset))
            {
                // Per-section colorway: each section takes its own color (or the whole-creature
                // tint); sections the preset doesn't name are left at the original palette.
                foreach (var section in sections)
                {
                    var c = preset.ColorFor(section.Name);
                    if (c.HasValue)
                        ApplySection(binData, paletteIndex, section, c.Value);
                }
                return true;
            }

            // User themes are tier-agnostic, saved under the family's editor key.
            var editorKey = MonsterThemeRegistry.ForTierKey(tierKey)?.EditorKey;
            if (userThemes != null && editorKey != null && userThemes.IsUserTheme(editorKey, themeName))
            {
                var userPalette = userThemes.LoadTheme(editorKey, themeName);
                if (userPalette != null && userPalette.Length >= 32)
                {
                    foreach (var section in sections)
                        ApplyUserPaletteSection(binData, paletteIndex, section, userPalette);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Copies a user theme's section colors (from its palette-0) into a tier's palette.</summary>
        public static void ApplyUserPaletteSection(byte[] binData, int paletteIndex, JobSection section, byte[] userPalette)
        {
            int palOff = paletteIndex * 32;
            foreach (var idx in section.Indices)
            {
                int src = idx * 2;           // user palette stores the edited monster as palette 0
                int dst = palOff + idx * 2;
                if (src + 1 < userPalette.Length && dst + 1 < binData.Length)
                {
                    binData[dst] = userPalette[src];
                    binData[dst + 1] = userPalette[src + 1];
                }
            }
        }

        public static void ApplySection(byte[] binData, int paletteIndex, JobSection section, Color baseColor)
        {
            int palOff = paletteIndex * 32;

            var colors = new Dictionary<int, Color>();
            foreach (var idx in section.Indices)
                colors[idx] = ReadColor(binData, palOff, idx);

            int primary = section.PrimaryIndex ?? section.Indices[0];
            if (!colors.ContainsKey(primary)) primary = section.Indices[0];

            var gen = new RelativeShadeGenerator(colors, primary, section.ShadeMode);
            foreach (var idx in section.Indices)
                WriteColor(binData, palOff, idx, gen.GenerateShade(idx, baseColor));
        }

        private static Color ReadColor(byte[] d, int palOff, int idx)
        {
            int o = palOff + idx * 2;
            ushort v = (ushort)(d[o] | (d[o + 1] << 8));
            int r = (v & 0x1F) << 3;
            int g = ((v >> 5) & 0x1F) << 3;
            int b = ((v >> 10) & 0x1F) << 3;
            return Color.FromArgb(r, g, b);
        }

        private static void WriteColor(byte[] d, int palOff, int idx, Color c)
        {
            ushort v = (ushort)(((c.R >> 3) & 0x1F) | (((c.G >> 3) & 0x1F) << 5) | (((c.B >> 3) & 0x1F) << 10));
            int o = palOff + idx * 2;
            d[o] = (byte)(v & 0xFF);
            d[o + 1] = (byte)((v >> 8) & 0xFF);
        }
    }
}
