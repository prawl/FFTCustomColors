using System;
using System.Drawing;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Provides pre-computed CLUTData palettes for built-in Ramza themes.
    /// These palettes are applied to charclut.nxd when a built-in theme is activated.
    /// </summary>
    public class RamzaBuiltInThemePalettes
    {

        // Original palettes from charclut.nxd (16 colors x 3 RGB values = 48 integers)
        // These were extracted from the base charclut.sqlite database using:
        // SELECT Key, Key2, CLUTData FROM CharCLUT WHERE Key IN (1, 2, 3) AND Key2 = 0
        private static readonly int[] OriginalChapter1Palette = new int[]
        {
            0, 0, 0,           // 0: Transparent
            40, 32, 32,        // 1: Dark outline
            224, 216, 208,     // 2: Light highlights
            40, 56, 72,        // 3: Dark blue armor
            48, 72, 104,       // 4: Medium blue armor
            56, 96, 128,       // 5: Light blue armor
            80, 128, 184,      // 6: Bright blue armor
            72, 48, 40,        // 7: Boots/leather dark
            96, 56, 40,        // 8: Boots/leather mid
            144, 80, 40,       // 9: Leather/brown
            112, 64, 40,       // 10: Leather/brown mid
            184, 120, 40,      // 11: Gold/yellow accent
            216, 152, 72,      // 12: Gold highlight
            160, 104, 40,      // 13: Skin shadow
            200, 136, 80,      // 14: Skin mid
            232, 192, 128      // 15: Skin highlight
        };

        private static readonly int[] OriginalChapter23Palette = new int[]
        {
            0, 0, 0,           // 0: Transparent
            40, 32, 32,        // 1: Dark outline
            224, 216, 192,     // 2: Light highlights
            72, 40, 88,        // 3: Dark purple armor
            104, 72, 144,      // 4: Medium purple armor
            144, 104, 208,     // 5: Light purple armor
            64, 64, 56,        // 6: Gray cloth/cape
            120, 112, 104,     // 7: Light gray cloth
            168, 160, 152,     // 8: Lighter cloth
            80, 32, 8,         // 9: Brown/leather dark
            128, 56, 8,        // 10: Brown/leather
            104, 64, 24,       // 11: Brown mid
            160, 96, 24,       // 12: Yellow/gold
            216, 160, 80,      // 13: Skin shadow
            200, 128, 64,      // 14: Skin mid
            232, 192, 128      // 15: Skin highlight
        };

        private static readonly int[] OriginalChapter4Palette = new int[]
        {
            0, 0, 0,           // 0: Transparent
            40, 32, 32,        // 1: Dark outline
            224, 216, 192,     // 2: Light highlights
            32, 64, 88,        // 3: Dark teal armor
            40, 96, 120,       // 4: Medium teal armor
            64, 136, 152,      // 5: Light teal armor
            64, 56, 56,        // 6: Gray cloth/cape
            112, 96, 80,       // 7: Brown cloth
            176, 160, 136,     // 8: Light cloth
            80, 32, 8,         // 9: Brown/leather dark
            128, 56, 8,        // 10: Brown/leather
            104, 64, 24,       // 11: Brown mid
            160, 96, 24,       // 12: Yellow/gold
            216, 160, 80,      // 13: Skin shadow
            200, 128, 64,      // 14: Skin mid
            232, 192, 128      // 15: Skin highlight
        };

        public RamzaBuiltInThemePalettes()
        {
        }

        /// <summary>
        /// Checks if the given theme is a built-in Ramza theme.
        /// </summary>
        public bool IsBuiltInTheme(string themeName)
        {
            return themeName == "dark_knight" ||
                   themeName == "white_heretic" ||
                   themeName == "crimson_blade";
        }

        /// <summary>
        /// Gets the CLUTData for a built-in theme and chapter combination.
        /// Returns null if the theme is not a built-in theme.
        /// </summary>
        public int[] GetThemePalette(string themeName, int chapter)
        {
            if (!IsBuiltInTheme(themeName))
                return null;

            // Get the original palette for this chapter
            int[] originalPalette = GetOriginalPalette(chapter);
            if (originalPalette == null)
                return null;

            // Apply the color transformation based on chapter
            return TransformPalette(originalPalette, themeName, chapter);
        }

        /// <summary>
        /// Gets the original palette for a chapter.
        /// </summary>
        public int[] GetOriginalPalette(int chapter)
        {
            return chapter switch
            {
                1 => (int[])OriginalChapter1Palette.Clone(),
                2 => (int[])OriginalChapter23Palette.Clone(),
                4 => (int[])OriginalChapter4Palette.Clone(),
                _ => null
            };
        }

        /// <summary>
        /// Gets palettes for all three Ramza chapters based on their individual theme selections.
        /// This fixes the bug where all chapters were getting the same theme applied.
        /// </summary>
        /// <param name="ch1Theme">Theme for Chapter 1</param>
        /// <param name="ch23Theme">Theme for Chapter 2/3</param>
        /// <param name="ch4Theme">Theme for Chapter 4</param>
        /// <returns>Tuple of palettes for each chapter</returns>
        public (int[] ch1Palette, int[] ch23Palette, int[] ch4Palette) GetChapterPalettes(
            string ch1Theme, string ch23Theme, string ch4Theme)
        {
            var ch1Palette = GetPaletteForTheme(ch1Theme, 1);
            var ch23Palette = GetPaletteForTheme(ch23Theme, 2);
            var ch4Palette = GetPaletteForTheme(ch4Theme, 4);

            return (ch1Palette, ch23Palette, ch4Palette);
        }

        /// <summary>
        /// Gets the appropriate palette for a theme and chapter combination.
        /// </summary>
        private int[] GetPaletteForTheme(string theme, int chapter)
        {
            if (theme == "original" || string.IsNullOrEmpty(theme))
            {
                return GetOriginalPalette(chapter);
            }

            if (IsBuiltInTheme(theme))
            {
                return GetThemePalette(theme, chapter);
            }

            // For user themes, return original (user themes are applied separately)
            return GetOriginalPalette(chapter);
        }

        /// <summary>
        /// Transforms a palette by applying theme-specific colors to armor indices.
        /// CLUTData palette indices vary by chapter:
        /// - Chapter 1: Armor is indices 3, 4, 5, 6 (all are blue/armor colors)
        /// - Chapter 2/3: Armor is indices 3, 4, 5 only (index 6 is gray cloth)
        /// - Chapter 4: Armor is indices 3, 4, 5 only (index 6 is gray cloth)
        /// </summary>
        private int[] TransformPalette(int[] originalPalette, string themeName, int chapter)
        {
            // Start with a copy of the original palette
            int[] transformedPalette = (int[])originalPalette.Clone();

            // Chapter 1 has 4 armor colors (3-6), Chapters 2/3 and 4 have only 3 (3-5)
            var armorIndices = chapter == 1
                ? new[] { 3, 4, 5, 6 }
                : new[] { 3, 4, 5 };

            foreach (int i in armorIndices)
            {
                int offset = i * 3;
                var transformedColor = GetThemeArmorColor(themeName, i);

                transformedPalette[offset] = transformedColor.R;
                transformedPalette[offset + 1] = transformedColor.G;
                transformedPalette[offset + 2] = transformedColor.B;
            }

            return transformedPalette;
        }

        /// <summary>
        /// Gets the armor color for a specific theme and palette index.
        /// Index 3 = darkest, Index 6 = brightest
        /// </summary>
        private Color GetThemeArmorColor(string themeName, int paletteIndex)
        {
            return themeName.ToLower() switch
            {
                "crimson_blade" => paletteIndex switch
                {
                    3 => Color.FromArgb(80, 16, 24),      // Dark crimson
                    4 => Color.FromArgb(136, 24, 32),     // Medium crimson
                    5 => Color.FromArgb(184, 40, 48),     // Light crimson
                    6 => Color.FromArgb(220, 64, 72),     // Bright crimson
                    _ => Color.Black
                },
                "white_heretic" => paletteIndex switch
                {
                    3 => Color.FromArgb(105, 105, 105),   // Dark gray
                    4 => Color.FromArgb(140, 140, 140),   // Medium gray
                    5 => Color.FromArgb(189, 189, 189),   // Light gray
                    6 => Color.FromArgb(230, 230, 230),   // Near white
                    _ => Color.Black
                },
                "dark_knight" => paletteIndex switch
                {
                    3 => Color.FromArgb(10, 10, 20),      // Near black
                    4 => Color.FromArgb(20, 25, 40),      // Very dark blue
                    5 => Color.FromArgb(30, 40, 60),      // Dark blue
                    6 => Color.FromArgb(40, 50, 80),      // Medium dark blue
                    _ => Color.Black
                },
                _ => Color.Black
            };
        }
    }
}
