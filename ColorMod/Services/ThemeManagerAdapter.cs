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

        // Characters that use standard sprite-based theming (not Ramza which uses TEX files)
        private static readonly string[] StandardCharacters = new[]
        {
            "Orlandeau", "Agrias", "Cloud", "Mustadio", "Marach",
            "Beowulf", "Meliadoul", "Rapha", "Reis"
        };

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

            // Apply initial themes for all standard characters
            foreach (var character in StandardCharacters)
            {
                ApplyInitialCharacterTheme(character);
            }
        }

        #region Generic Character Theme Methods

        /// <summary>
        /// Cycles the theme for a character and applies it.
        /// This is the primary method - character-specific methods delegate to this.
        /// </summary>
        /// <param name="characterName">The character name (e.g., "Orlandeau", "Agrias")</param>
        public void CycleCharacterTheme(string characterName)
        {
            if (characterName == "Ramza")
            {
                CycleRamzaTheme();
                return;
            }

            var nextTheme = _themeService.CycleTheme(characterName);
            _storyCharacterManager.SetCurrentTheme(characterName, nextTheme);
            ModLogger.Log($"{characterName} theme: {nextTheme}");
            ApplyCharacterTheme(characterName, nextTheme);
        }

        /// <summary>
        /// Applies a theme to a character.
        /// For standard characters, this applies the theme service and copies sprites.
        /// </summary>
        /// <param name="characterName">The character name</param>
        /// <param name="theme">The theme to apply</param>
        public void ApplyCharacterTheme(string characterName, string theme)
        {
            if (characterName == "Ramza")
            {
                ApplyRamzaTheme(theme);
                return;
            }

            _themeService.ApplyTheme(characterName, theme);
            CopyCharacterSprites(characterName, theme);
        }

        /// <summary>
        /// Applies the initial theme for a character from stored configuration.
        /// </summary>
        /// <param name="characterName">The character name</param>
        private void ApplyInitialCharacterTheme(string characterName)
        {
            var theme = _storyCharacterManager.GetCurrentTheme(characterName);
            ModLogger.LogDebug($"ApplyInitial{characterName}Theme - theme: {theme}");
            ApplyCharacterTheme(characterName, theme);
        }

        #endregion

        #region Ramza-Specific Methods (Special TEX handling)

        public void CycleRamzaTheme()
        {
            var nextTheme = _themeService.CycleTheme("Ramza");
            _storyCharacterManager.SetCurrentTheme("Ramza", nextTheme);
            ModLogger.Log($"Ramza theme: {nextTheme}");
            ApplyRamzaTheme(nextTheme);
        }

        private void ApplyInitialRamzaTheme()
        {
            // Get the actual configured theme from ConfigurationService
            var pathResolver = new SimplePathResolver(_sourcePath, _modPath);
            var configService = new ConfigurationService(pathResolver);
            var config = configService.LoadConfig();

            // Try to get RamzaChapter1 theme from config
            var theme = config?.GetStoryCharacterTheme("RamzaChapter1") ?? "original";

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

        private void ApplyRamzaTheme(string theme)
        {
            _themeService.ApplyTheme("Ramza", theme);

            // Load config to get each chapter's theme selection
            var pathResolver = new SimplePathResolver(_sourcePath, _modPath);
            var configService = new ConfigurationService(pathResolver);
            var config = configService.LoadConfig();

            var ch1Theme = config?.GetStoryCharacterTheme("RamzaChapter1") ?? "original";
            var ch23Theme = config?.GetStoryCharacterTheme("RamzaChapter23") ?? "original";
            var ch4Theme = config?.GetStoryCharacterTheme("RamzaChapter4") ?? "original";

            ModLogger.LogDebug($"Applying Ramza themes: Ch1={ch1Theme}, Ch23={ch23Theme}, Ch4={ch4Theme}");

            // Tex files are ONLY used for built-in themes (dark_knight, white_heretic, crimson_blade)
            // User themes and "original" use NXD palettes only - tex files would interfere
            var builtInPalettes = new RamzaBuiltInThemePalettes();
            var hasBuiltInTheme = builtInPalettes.IsBuiltInTheme(ch1Theme) ||
                                  builtInPalettes.IsBuiltInTheme(ch23Theme) ||
                                  builtInPalettes.IsBuiltInTheme(ch4Theme);

            var modRootPath = _modPath;
            var gameTexPath = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "system", "ffto", "g2d");
            var texSwapper = new RamzaTexSwapper(modRootPath, gameTexPath);

            if (hasBuiltInTheme)
            {
                // Only swap tex files for built-in themes
                // For chapters with user themes or "original", pass "original" to remove their tex files
                var ch1TexTheme = builtInPalettes.IsBuiltInTheme(ch1Theme) ? ch1Theme : "original";
                var ch23TexTheme = builtInPalettes.IsBuiltInTheme(ch23Theme) ? ch23Theme : "original";
                var ch4TexTheme = builtInPalettes.IsBuiltInTheme(ch4Theme) ? ch4Theme : "original";

                ModLogger.LogDebug($"Tex themes (built-in only): Ch1={ch1TexTheme}, Ch23={ch23TexTheme}, Ch4={ch4TexTheme}");
                texSwapper.SwapTexFilesPerChapter(ch1TexTheme, ch23TexTheme, ch4TexTheme);
            }
            else
            {
                // No built-in themes - remove all tex files to let NXD palettes take effect
                ModLogger.LogDebug("No built-in themes, removing all Ramza tex files");
                texSwapper.RestoreOriginalTexFiles();
            }

            // Apply NXD palettes for all chapters
            ApplyBuiltInThemeToNxd(theme);
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
        /// Applies built-in Ramza theme palettes to the charclut.nxd file.
        /// Reads each chapter's configured theme and applies the appropriate palette.
        /// This ensures each chapter maintains its own theme selection.
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

                // Load config to get each chapter's theme selection
                var pathResolver = new SimplePathResolver(_sourcePath, _modPath);
                var configService = new ConfigurationService(pathResolver);
                var config = configService.LoadConfig();

                // Get each chapter's configured theme (fallback to "original" if not set)
                var ch1Theme = config?.GetStoryCharacterTheme("RamzaChapter1") ?? "original";
                var ch23Theme = config?.GetStoryCharacterTheme("RamzaChapter23") ?? "original";
                var ch4Theme = config?.GetStoryCharacterTheme("RamzaChapter4") ?? "original";

                ModLogger.LogDebug($"Applying per-chapter themes: Ch1={ch1Theme}, Ch23={ch23Theme}, Ch4={ch4Theme}");

                // Get palettes for each chapter based on its configured theme
                var (chapter1Palette, chapter23Palette, chapter4Palette) =
                    builtInPalettes.GetChapterPalettes(ch1Theme, ch23Theme, ch4Theme);

                var themeSaver = new RamzaThemeSaver();
                var success = themeSaver.ApplyAllChaptersClutData(
                    chapter1Palette,
                    chapter23Palette,
                    chapter4Palette,
                    _modPath);

                if (success)
                {
                    ModLogger.Log($"Successfully updated charclut.nxd with per-chapter themes");
                }
                else
                {
                    ModLogger.LogWarning($"Failed to update charclut.nxd");
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"Error applying theme to NXD: {ex.Message}");
            }
        }

        #endregion

        #region Backward Compatibility Methods (delegate to generic methods)

        // These methods are kept for backward compatibility with existing code
        // They all delegate to the generic CycleCharacterTheme method

        public void CycleOrlandeauTheme() => CycleCharacterTheme("Orlandeau");
        public void CycleAgriasTheme() => CycleCharacterTheme("Agrias");
        public void CycleCloudTheme() => CycleCharacterTheme("Cloud");
        public void CycleMustadioTheme() => CycleCharacterTheme("Mustadio");
        public void CycleMarachTheme() => CycleCharacterTheme("Marach");
        public void CycleBeowulfTheme() => CycleCharacterTheme("Beowulf");
        public void CycleMeliadoulTheme() => CycleCharacterTheme("Meliadoul");
        public void CycleRaphaTheme() => CycleCharacterTheme("Rapha");
        public void CycleReisTheme() => CycleCharacterTheme("Reis");

        #endregion

        #region Sprite Operations

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

        #endregion

        #region SimplePathResolver

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

        #endregion
    }
}
