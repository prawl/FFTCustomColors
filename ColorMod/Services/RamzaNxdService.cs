using System;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Orchestrates Ramza NXD patching operations.
    /// Coordinates between RamzaThemeSaver and RamzaBuiltInThemePalettes to apply themes.
    /// Extracted from ConfigBasedSpriteManager to follow Single Responsibility Principle.
    /// </summary>
    public class RamzaNxdService
    {
        private readonly string _modPath;
        private readonly RamzaBuiltInThemePalettes _builtInPalettes;
        private readonly RamzaThemeSaver _themeSaver;

        public RamzaNxdService(string modPath)
        {
            _modPath = modPath ?? throw new ArgumentNullException(nameof(modPath));
            _builtInPalettes = new RamzaBuiltInThemePalettes();
            _themeSaver = new RamzaThemeSaver();
        }

        /// <summary>
        /// Constructor for testing with injected dependencies.
        /// </summary>
        public RamzaNxdService(string modPath, RamzaBuiltInThemePalettes builtInPalettes, RamzaThemeSaver themeSaver)
        {
            _modPath = modPath ?? throw new ArgumentNullException(nameof(modPath));
            _builtInPalettes = builtInPalettes ?? throw new ArgumentNullException(nameof(builtInPalettes));
            _themeSaver = themeSaver ?? throw new ArgumentNullException(nameof(themeSaver));
        }

        /// <summary>
        /// Applies all Ramza chapters' themes to the NXD at once.
        /// This ensures each chapter maintains its own theme selection.
        /// </summary>
        public bool ApplyAllChaptersToNxd(Config config)
        {
            try
            {
                var ch1Theme = config?.RamzaChapter1 ?? "original";
                var ch23Theme = config?.RamzaChapter23 ?? "original";
                var ch4Theme = config?.RamzaChapter4 ?? "original";

                ModLogger.Log($"[RAMZA_ALL_CHAPTERS] Applying per-chapter themes: Ch1={ch1Theme}, Ch23={ch23Theme}, Ch4={ch4Theme}");

                var (ch1Palette, ch23Palette, ch4Palette) = _builtInPalettes.GetChapterPalettes(ch1Theme, ch23Theme, ch4Theme);

                var success = _themeSaver.ApplyAllChaptersClutData(ch1Palette, ch23Palette, ch4Palette, _modPath);

                if (success)
                {
                    ModLogger.LogSuccess($"[RAMZA_ALL_CHAPTERS] Successfully patched charclut.nxd with per-chapter themes");
                }
                else
                {
                    ModLogger.LogWarning($"[RAMZA_ALL_CHAPTERS] Failed to patch charclut.nxd");
                }

                return success;
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[RAMZA_ALL_CHAPTERS] Error applying themes: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Applies a built-in Ramza theme by patching the NXD with pre-computed palettes.
        /// </summary>
        /// <param name="characterName">Ramza character name (e.g., "RamzaChapter1").</param>
        /// <param name="themeName">Built-in theme name (dark_knight, white_heretic, crimson_blade).</param>
        public bool ApplyBuiltInThemeToNxd(string characterName, string themeName)
        {
            try
            {
                ModLogger.Log($"[RAMZA_BUILTIN] Applying built-in theme '{themeName}' for {characterName}");

                int chapter = GetChapterFromCharacterName(characterName);
                var clutData = _builtInPalettes.GetThemePalette(themeName.ToLower(), chapter);

                if (clutData == null)
                {
                    ModLogger.LogWarning($"[RAMZA_BUILTIN] No palette found for {themeName}/{chapter}");
                    return false;
                }

                LogArmorColors(themeName, chapter, clutData);

                var success = _themeSaver.ApplyClutData(chapter, clutData, _modPath);

                if (success)
                {
                    ModLogger.LogSuccess($"[RAMZA_BUILTIN] Successfully patched charclut.nxd for {characterName}/{themeName}");
                }
                else
                {
                    ModLogger.LogWarning($"[RAMZA_BUILTIN] Failed to patch charclut.nxd for {characterName}/{themeName}");
                }

                return success;
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[RAMZA_BUILTIN] Error applying built-in theme: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Applies a user theme palette to the charclut.nxd file.
        /// </summary>
        /// <param name="characterName">Ramza character name (e.g., "RamzaChapter1").</param>
        /// <param name="paletteData">Palette data bytes (512 bytes).</param>
        public bool ApplyUserThemeToNxd(string characterName, byte[] paletteData)
        {
            try
            {
                ModLogger.Log($"[RAMZA_NXD] Applying user theme NXD palette for {characterName}");

                int chapter = GetChapterFromCharacterName(characterName);
                var clutData = _themeSaver.ConvertPaletteToClutData(paletteData);
                var success = _themeSaver.ApplyClutData(chapter, clutData, _modPath);

                if (success)
                {
                    ModLogger.LogSuccess($"[RAMZA_NXD] Successfully patched charclut.nxd for {characterName}");
                }
                else
                {
                    ModLogger.LogWarning($"[RAMZA_NXD] Failed to patch charclut.nxd for {characterName}");
                }

                return success;
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"[RAMZA_NXD] Error patching NXD: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if the character name is a Ramza chapter.
        /// </summary>
        public bool IsRamzaChapter(string characterName)
        {
            var lower = characterName.ToLower();
            return lower == "ramzachapter1" || lower == "ramzachapter23" || lower == "ramzachapter4";
        }

        /// <summary>
        /// Checks if the theme is a built-in Ramza theme.
        /// </summary>
        public bool IsBuiltInRamzaTheme(string themeName)
        {
            return _builtInPalettes.IsBuiltInTheme(themeName.ToLower());
        }

        private int GetChapterFromCharacterName(string characterName)
        {
            return characterName.ToLower() switch
            {
                "ramzachapter1" => 1,
                "ramzachapter23" => 2,
                "ramzachapter4" => 4,
                _ => throw new ArgumentException($"Invalid Ramza chapter: {characterName}")
            };
        }

        private void LogArmorColors(string themeName, int chapter, int[] clutData)
        {
            ModLogger.Log($"[RAMZA_BUILTIN] Armor colors for {themeName}/Ch{chapter}:");
            for (int i = 3; i <= 6; i++)
            {
                int offset = i * 3;
                if (offset + 2 < clutData.Length)
                {
                    ModLogger.Log($"[RAMZA_BUILTIN]   Index {i}: RGB({clutData[offset]}, {clutData[offset + 1]}, {clutData[offset + 2]})");
                }
            }
        }
    }
}
