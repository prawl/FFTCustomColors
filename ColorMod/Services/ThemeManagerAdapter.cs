using System;
using System.IO;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Interfaces;
using FFTColorCustomizer.ThemeEditor;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Adapter that maintains backward compatibility with ThemeManager
    /// while using the new ThemeService internally
    /// </summary>
    public class ThemeManagerAdapter
    {
        private readonly IThemeService _themeService;
        private readonly StoryCharacterThemeManager _storyCharacterManager;
        private readonly string _sourcePath;
        private readonly string _modPath;

        public ThemeManagerAdapter(string sourcePath, string modPath)
        {
            _sourcePath = sourcePath;
            _modPath = modPath;

            // Create services
            var pathResolver = new SimplePathResolver(sourcePath, modPath);
            var configService = new ConfigurationService(pathResolver);
            _themeService = new ThemeService(pathResolver, configService);

            // Keep StoryCharacterThemeManager for backward compatibility
            _storyCharacterManager = new StoryCharacterThemeManager(sourcePath);
        }

        public StoryCharacterThemeManager GetStoryCharacterManager()
        {
            return _storyCharacterManager;
        }

        public void ApplyInitialThemes()
        {
            ModLogger.LogDebug("ThemeManagerAdapter.ApplyInitialThemes() called");
            ApplyInitialRamzaTheme();
            ApplyInitialOrlandeauTheme();
            ApplyInitialAgriasTheme();
            ApplyInitialCloudTheme();
            ApplyInitialMustadioTheme();
            ApplyInitialMarachTheme();
            ApplyInitialBeowulfTheme();
            ApplyInitialMeliadoulTheme();
            ApplyInitialRaphaTheme();
            ApplyInitialReisTheme();
        }

        public void CycleRamzaTheme()
        {
            var nextTheme = _themeService.CycleTheme("Ramza");
            _storyCharacterManager.SetCurrentTheme("Ramza", nextTheme);
            ModLogger.Log($"Ramza theme: {nextTheme}");
            ApplyRamzaTheme(nextTheme);
        }

        public void CycleOrlandeauTheme()
        {
            var nextTheme = _themeService.CycleTheme("Orlandeau");
            _storyCharacterManager.SetCurrentTheme("Orlandeau", nextTheme);
            ModLogger.Log($"Orlandeau theme: {nextTheme}");
            ApplyOrlandeauTheme(nextTheme);
        }

        public void CycleAgriasTheme()
        {
            var nextTheme = _themeService.CycleTheme("Agrias");
            _storyCharacterManager.SetCurrentTheme("Agrias", nextTheme);
            ModLogger.Log($"================================================");
            ModLogger.Log($"    AGRIAS THEME CHANGED TO: {nextTheme}");
            ModLogger.Log($"================================================");
            ApplyAgriasTheme(nextTheme);
        }

        public void CycleCloudTheme()
        {
            var nextTheme = _themeService.CycleTheme("Cloud");
            _storyCharacterManager.SetCurrentTheme("Cloud", nextTheme);
            ModLogger.Log($"Cloud theme: {nextTheme}");
            ApplyCloudTheme(nextTheme);
        }

        public void CycleMustadioTheme()
        {
            var nextTheme = _themeService.CycleTheme("Mustadio");
            _storyCharacterManager.SetCurrentTheme("Mustadio", nextTheme);
            ModLogger.Log($"Mustadio theme: {nextTheme}");
            ApplyMustadioTheme(nextTheme);
        }

        public void CycleMarachTheme()
        {
            var nextTheme = _themeService.CycleTheme("Marach");
            _storyCharacterManager.SetCurrentTheme("Marach", nextTheme);
            ModLogger.Log($"Marach theme: {nextTheme}");
            ApplyMarachTheme(nextTheme);
        }

        public void CycleBeowulfTheme()
        {
            var nextTheme = _themeService.CycleTheme("Beowulf");
            _storyCharacterManager.SetCurrentTheme("Beowulf", nextTheme);
            ModLogger.Log($"Beowulf theme: {nextTheme}");
            ApplyBeowulfTheme(nextTheme);
        }

        public void CycleMeliadoulTheme()
        {
            var nextTheme = _themeService.CycleTheme("Meliadoul");
            _storyCharacterManager.SetCurrentTheme("Meliadoul", nextTheme);
            ModLogger.Log($"Meliadoul theme: {nextTheme}");
            ApplyMeliadoulTheme(nextTheme);
        }

        public void CycleRaphaTheme()
        {
            var nextTheme = _themeService.CycleTheme("Rapha");
            _storyCharacterManager.SetCurrentTheme("Rapha", nextTheme);
            ModLogger.Log($"Rapha theme: {nextTheme}");
            ApplyRaphaTheme(nextTheme);
        }

        public void CycleReisTheme()
        {
            var nextTheme = _themeService.CycleTheme("Reis");
            _storyCharacterManager.SetCurrentTheme("Reis", nextTheme);
            ModLogger.Log($"Reis theme: {nextTheme}");
            ApplyReisTheme(nextTheme);
        }

        private void ApplyInitialRamzaTheme()
        {
            // Get the actual configured theme from ConfigurationService
            var pathResolver = new SimplePathResolver(_sourcePath, _modPath);
            var configService = new ConfigurationService(pathResolver);
            var config = configService.LoadConfig();

            // Try to get RamzaChapter1 theme from config
            var theme = config?.RamzaChapter1 ?? "original";

            // If not found in config, fall back to StoryCharacterThemeManager
            if (string.IsNullOrEmpty(theme))
            {
                theme = _storyCharacterManager.GetCurrentTheme("RamzaChapter1");
                if (string.IsNullOrEmpty(theme))
                {
                    theme = _storyCharacterManager.GetCurrentTheme("Ramza");
                }
            }

            ModLogger.LogDebug($"ApplyInitialRamzaTheme - theme from config: {theme}");
            ApplyRamzaTheme(theme);
        }

        private void ApplyInitialOrlandeauTheme()
        {
            var theme = _storyCharacterManager.GetCurrentTheme("Orlandeau");
            ModLogger.LogDebug($"ApplyInitialOrlandeauTheme - theme: {theme}");
            ApplyOrlandeauTheme(theme);
        }

        private void ApplyInitialAgriasTheme()
        {
            var theme = _storyCharacterManager.GetCurrentTheme("Agrias");
            ModLogger.LogDebug($"ApplyInitialAgriasTheme - theme: {theme}");
            ApplyAgriasTheme(theme);
        }

        private void ApplyInitialCloudTheme()
        {
            var theme = _storyCharacterManager.GetCurrentTheme("Cloud");
            ModLogger.LogDebug($"ApplyInitialCloudTheme - theme: {theme}");
            ApplyCloudTheme(theme);
        }

        private void ApplyInitialMustadioTheme()
        {
            var theme = _storyCharacterManager.GetCurrentTheme("Mustadio");
            ModLogger.LogDebug($"ApplyInitialMustadioTheme - theme: {theme}");
            ApplyMustadioTheme(theme);
        }

        private void ApplyInitialMarachTheme()
        {
            var theme = _storyCharacterManager.GetCurrentTheme("Marach");
            ModLogger.LogDebug($"ApplyInitialMarachTheme - theme: {theme}");
            ApplyMarachTheme(theme);
        }

        private void ApplyInitialBeowulfTheme()
        {
            var theme = _storyCharacterManager.GetCurrentTheme("Beowulf");
            ModLogger.LogDebug($"ApplyInitialBeowulfTheme - theme: {theme}");
            ApplyBeowulfTheme(theme);
        }

        private void ApplyInitialMeliadoulTheme()
        {
            var theme = _storyCharacterManager.GetCurrentTheme("Meliadoul");
            ModLogger.LogDebug($"ApplyInitialMeliadoulTheme - theme: {theme}");
            ApplyMeliadoulTheme(theme);
        }

        private void ApplyInitialRaphaTheme()
        {
            var theme = _storyCharacterManager.GetCurrentTheme("Rapha");
            ModLogger.LogDebug($"ApplyInitialRaphaTheme - theme: {theme}");
            ApplyRaphaTheme(theme);
        }

        private void ApplyInitialReisTheme()
        {
            var theme = _storyCharacterManager.GetCurrentTheme("Reis");
            ModLogger.LogDebug($"ApplyInitialReisTheme - theme: {theme}");
            ApplyReisTheme(theme);
        }

        private void ApplyRamzaTheme(string theme)
        {
            _themeService.ApplyTheme("Ramza", theme);

            // Ramza uses tex files - themes are stored in RamzaThemes folder at mod root
            // - modRootPath: Root of the deployed mod (contains RamzaThemes folder)
            // - gameTexPath: Where the game reads tex files from (g2d directory)
            var modRootPath = _modPath;
            var gameTexPath = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "system", "ffto", "g2d");

            ModLogger.LogDebug($"Applying Ramza tex theme: {theme}");
            ModLogger.LogDebug($"Mod root: {modRootPath}");
            ModLogger.LogDebug($"Game tex path: {gameTexPath}");

            var texSwapper = new RamzaTexSwapper(modRootPath, gameTexPath);

            if (theme == "original")
            {
                texSwapper.RestoreOriginalTexFiles();
                ModLogger.Log($"Restored original Ramza tex files from original_backup/");
                // Restore original NXD palettes
                ApplyBuiltInThemeToNxd("original");
            }
            else
            {
                texSwapper.SwapTexFilesForTheme(theme);
                ModLogger.Log($"Swapped Ramza tex files to theme: {theme} from {theme}/");

                // Apply NXD palette for the theme
                var builtInPalettes = new RamzaBuiltInThemePalettes();
                if (builtInPalettes.IsBuiltInTheme(theme))
                {
                    // Built-in themes use computed palettes
                    ApplyBuiltInThemeToNxd(theme);
                }
                else
                {
                    // User themes need their saved palette applied to NXD
                    ApplyUserThemeToNxd(theme);
                }
            }
        }

        /// <summary>
        /// Applies a user-created Ramza theme's palette to the charclut.nxd file.
        /// Loads the saved palette from UserThemes folder and patches the NXD.
        /// </summary>
        private void ApplyUserThemeToNxd(string theme)
        {
            try
            {
                var userThemeService = new UserThemeService(_modPath);
                var themeSaver = new RamzaThemeSaver();

                // Try to load palette for each Ramza chapter that has this theme
                var chapters = new[] { ("RamzaChapter1", 1), ("RamzaChapter23", 2), ("RamzaChapter4", 4) };
                bool anyApplied = false;

                foreach (var (chapterName, chapterNum) in chapters)
                {
                    if (userThemeService.IsUserTheme(chapterName, theme))
                    {
                        var palettePath = userThemeService.GetUserThemePalettePath(chapterName, theme);
                        if (!string.IsNullOrEmpty(palettePath) && File.Exists(palettePath))
                        {
                            var paletteData = File.ReadAllBytes(palettePath);
                            ModLogger.Log($"Applying user theme '{theme}' palette for {chapterName} from {palettePath}");

                            // Convert palette to CLUTData and apply to NXD
                            var clutData = themeSaver.ConvertPaletteToClutData(paletteData);
                            var success = themeSaver.ApplyClutData(chapterNum, clutData, _modPath);

                            if (success)
                            {
                                ModLogger.Log($"Successfully applied user theme NXD palette for {chapterName}");
                                anyApplied = true;
                            }
                            else
                            {
                                ModLogger.LogWarning($"Failed to apply user theme NXD palette for {chapterName}");
                            }
                        }
                    }
                }

                if (!anyApplied)
                {
                    ModLogger.LogWarning($"No user theme palettes found for theme: {theme}");
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"Error applying user theme to NXD: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies a built-in Ramza theme's palette to the charclut.nxd file.
        /// Only applies palettes for built-in themes (dark_knight, white_heretic, crimson_blade)
        /// and "original". User-created themes already have their palettes applied when saved.
        /// </summary>
        private void ApplyBuiltInThemeToNxd(string theme)
        {
            try
            {
                var builtInPalettes = new RamzaBuiltInThemePalettes();

                // Only update NXD for built-in themes and "original"
                // User themes already have their palette applied when saved via SaveRamzaTheme
                if (!builtInPalettes.IsBuiltInTheme(theme) && theme != "original")
                {
                    ModLogger.LogDebug($"Skipping NXD update for user theme: {theme}");
                    return;
                }

                var themeSaver = new RamzaThemeSaver();

                int[] chapter1Palette;
                int[] chapter23Palette;
                int[] chapter4Palette;

                if (builtInPalettes.IsBuiltInTheme(theme))
                {
                    // Get transformed palettes for the built-in theme
                    chapter1Palette = builtInPalettes.GetThemePalette(theme, 1);
                    chapter23Palette = builtInPalettes.GetThemePalette(theme, 2);
                    chapter4Palette = builtInPalettes.GetThemePalette(theme, 4);
                    ModLogger.Log($"Applying {theme} palette to charclut.nxd");
                }
                else
                {
                    // "original" theme - restore original palettes
                    chapter1Palette = builtInPalettes.GetOriginalPalette(1);
                    chapter23Palette = builtInPalettes.GetOriginalPalette(2);
                    chapter4Palette = builtInPalettes.GetOriginalPalette(4);
                    ModLogger.Log($"Restoring original palette in charclut.nxd");
                }

                var success = themeSaver.ApplyAllChaptersClutData(
                    chapter1Palette,
                    chapter23Palette,
                    chapter4Palette,
                    _modPath);

                if (success)
                {
                    ModLogger.Log($"Successfully updated charclut.nxd for theme: {theme}");
                }
                else
                {
                    ModLogger.LogWarning($"Failed to update charclut.nxd for theme: {theme}");
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"Error applying theme to NXD: {ex.Message}");
            }
        }

        private void ApplyOrlandeauTheme(string theme)
        {
            _themeService.ApplyTheme("Orlandeau", theme);
            // Copy sprite files as needed - this would use SpriteService
            CopyCharacterSprites("Orlandeau", theme);
        }

        private void ApplyAgriasTheme(string theme)
        {
            _themeService.ApplyTheme("Agrias", theme);
            CopyCharacterSprites("Agrias", theme);
        }

        private void ApplyCloudTheme(string theme)
        {
            _themeService.ApplyTheme("Cloud", theme);
            CopyCharacterSprites("Cloud", theme);
        }

        private void ApplyMustadioTheme(string theme)
        {
            _themeService.ApplyTheme("Mustadio", theme);
            CopyCharacterSprites("Mustadio", theme);
        }

        private void ApplyMarachTheme(string theme)
        {
            _themeService.ApplyTheme("Marach", theme);
            CopyCharacterSprites("Marach", theme);
        }

        private void ApplyBeowulfTheme(string theme)
        {
            _themeService.ApplyTheme("Beowulf", theme);
            CopyCharacterSprites("Beowulf", theme);
        }

        private void ApplyMeliadoulTheme(string theme)
        {
            _themeService.ApplyTheme("Meliadoul", theme);
            CopyCharacterSprites("Meliadoul", theme);
        }

        private void ApplyRaphaTheme(string theme)
        {
            _themeService.ApplyTheme("Rapha", theme);
            CopyCharacterSprites("Rapha", theme);
        }

        private void ApplyReisTheme(string theme)
        {
            _themeService.ApplyTheme("Reis", theme);
            CopyCharacterSprites("Reis", theme);
        }

        private void CopyCharacterSprites(string character, string theme)
        {
            // This implementation would use SpriteService
            // For now, copy files directly to maintain compatibility
            var spriteNames = GetSpriteNamesForCharacter(character);

            foreach (var spriteName in spriteNames)
            {
                // Try multiple directory structures for compatibility
                var possiblePaths = new[]
                {
                    // Character-specific theme directories (e.g., sprites_agrias_original)
                    Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit",
                        $"sprites_{character.ToLowerInvariant()}_{theme}", spriteName),
                    // Generic theme directories (e.g., sprites_original)
                    Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit",
                        $"sprites_{theme}", spriteName),
                    // Direct sprites path
                    Path.Combine(_sourcePath, $"sprites_{theme}", spriteName)
                };

                string sourcePath = null;
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        sourcePath = path;
                        break;
                    }
                }

                if (sourcePath != null)
                {
                    var destPath = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", spriteName);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                    File.Copy(sourcePath, destPath, true);
                    ModLogger.Log($"Copied {character} sprite from {sourcePath} to {destPath}");
                }
            }
        }

        /// <summary>
        /// Gets sprite file names for a character. For multi-sprite characters (Agrias, Mustadio, Reis),
        /// this returns all sprite files that need to be updated when applying a theme.
        /// </summary>
        public string[] GetSpriteNamesForCharacter(string character)
        {
            // Try to load from section mapping first (supports multi-sprite characters)
            var mappingsPath = Path.Combine(_modPath, "Data", "SectionMappings");
            var storyMappingPath = Path.Combine(mappingsPath, "Story", $"{character}.json");

            if (File.Exists(storyMappingPath))
            {
                try
                {
                    var mapping = ThemeEditor.SectionMappingLoader.LoadFromFile(storyMappingPath);
                    return mapping.Sprites;
                }
                catch
                {
                    // Fall back to hardcoded values if mapping fails to load
                }
            }

            // Fallback for characters without section mappings
            return character switch
            {
                "Ramza" => new[] { "battle_ramuza_spr.bin", "battle_ramuza2_spr.bin", "battle_ramuza3_spr.bin" },
                "Orlandeau" => new[] { "battle_oru_spr.bin" },
                "Cloud" => new[] { "battle_cloud_spr.bin" },
                "Marach" => new[] { "battle_mara_spr.bin" },
                "Beowulf" => new[] { "battle_beio_spr.bin" },
                "Meliadoul" => new[] { "battle_h80_spr.bin" },
                "Rapha" => new[] { "battle_h79_spr.bin" },
                _ => Array.Empty<string>()
            };
        }

        // Simple path resolver for the adapter
        private class SimplePathResolver : IPathResolver
        {
            private readonly string _sourcePath;
            private readonly string _modPath;

            public SimplePathResolver(string sourcePath, string modPath)
            {
                _sourcePath = sourcePath;
                _modPath = modPath;
            }

            public string ModRootPath => _modPath;
            public string SourcePath => _sourcePath;
            public string UserConfigPath => _modPath;

            public string GetConfigPath()
            {
                // First try User/Mods/paxtrick.fft.colorcustomizer directory (where user's config is saved)
                var reloadedDir = Path.GetDirectoryName(Path.GetDirectoryName(_modPath)); // Go up to Reloaded directory
                var userConfigPath = Path.Combine(reloadedDir, "User", "Mods", "paxtrick.fft.colorcustomizer", "Config.json");

                if (File.Exists(userConfigPath))
                {
                    ModLogger.LogDebug($"Loading config from User directory: {userConfigPath}");
                    return userConfigPath;
                }

                // Fall back to mod directory
                var modConfigPath = Path.Combine(_modPath, "Config.json");
                ModLogger.LogDebug($"Loading config from mod directory: {modConfigPath}");
                return modConfigPath;
            }

            public string GetDataPath(string relativePath)
            {
                return Path.Combine(_modPath, "Data", relativePath);
            }

            public string GetSpritePath(string characterName, string themeName, string spriteFileName)
            {
                return Path.Combine(_sourcePath, $"sprites_{themeName}", spriteFileName);
            }

            public string GetThemeDirectory(string characterName, string themeName)
            {
                return Path.Combine(_sourcePath, $"sprites_{themeName}");
            }

            public string GetPreviewImagePath(string characterName, string themeName)
            {
                return Path.Combine(_modPath, "Resources", "Previews", $"{characterName}_{themeName}.png");
            }

            public string ResolveFirstExistingPath(params string[] candidates)
            {
                foreach (var path in candidates)
                {
                    if (File.Exists(path) || Directory.Exists(path))
                        return path;
                }
                return candidates.Length > 0 ? candidates[0] : string.Empty;
            }

            public System.Collections.Generic.IEnumerable<string> GetAvailableThemes(string characterName)
            {
                var themesPath = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
                if (Directory.Exists(themesPath))
                {
                    var dirs = Directory.GetDirectories(themesPath, "sprites_*");
                    foreach (var dir in dirs)
                    {
                        yield return Path.GetFileName(dir).Replace("sprites_", "");
                    }
                }
            }
        }
    }
}
