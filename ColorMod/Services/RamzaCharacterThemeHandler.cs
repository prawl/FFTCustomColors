using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Interfaces;
using FFTColorCustomizer.ThemeEditor;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Theme handler for Ramza's multi-chapter TEX/NXD system.
    /// Ramza has unique theming requirements:
    /// - Three separate chapters (1, 2/3, 4) with independent themes
    /// - Uses TEX files for built-in themes (dark_knight, white_heretic, crimson_blade)
    /// - Uses NXD palettes for user themes and original
    /// </summary>
    public class RamzaCharacterThemeHandler : IMultiChapterCharacterHandler
    {
        private readonly string _sourcePath;
        private readonly string _modPath;
        private readonly IThemeService _themeService;
        private readonly StoryCharacterThemeManager _storyCharacterManager;
        private readonly RamzaBuiltInThemePalettes _builtInPalettes;
        private readonly RamzaThemeSaver _themeSaver;

        private static readonly string[] ChapterNames = { "RamzaChapter1", "RamzaChapter23", "RamzaChapter4" };

        public string CharacterName => "Ramza";

        public RamzaCharacterThemeHandler(
            string sourcePath,
            string modPath,
            IThemeService themeService,
            StoryCharacterThemeManager storyCharacterManager)
        {
            _sourcePath = sourcePath;
            _modPath = modPath;
            _themeService = themeService;
            _storyCharacterManager = storyCharacterManager;
            _builtInPalettes = new RamzaBuiltInThemePalettes();
            _themeSaver = new RamzaThemeSaver();
        }

        public string CycleTheme()
        {
            var nextTheme = _themeService.CycleTheme("Ramza");
            _storyCharacterManager.SetCurrentTheme("Ramza", nextTheme);
            ModLogger.Log($"Ramza theme: {nextTheme}");
            ApplyTheme(nextTheme);
            return nextTheme;
        }

        public void ApplyTheme(string themeName)
        {
            _themeService.ApplyTheme("Ramza", themeName);

            // Load config to get each chapter's theme selection
            var config = LoadConfig();

            var ch1Theme = config?.GetStoryCharacterTheme("RamzaChapter1") ?? "original";
            var ch23Theme = config?.GetStoryCharacterTheme("RamzaChapter23") ?? "original";
            var ch4Theme = config?.GetStoryCharacterTheme("RamzaChapter4") ?? "original";

            ModLogger.LogDebug($"Applying Ramza themes: Ch1={ch1Theme}, Ch23={ch23Theme}, Ch4={ch4Theme}");

            ApplyTexFiles(ch1Theme, ch23Theme, ch4Theme);
            ApplyNxdPalettes(themeName);
        }

        public string GetCurrentTheme()
        {
            return _storyCharacterManager.GetCurrentTheme("Ramza");
        }

        public IEnumerable<string> GetAvailableThemes()
        {
            return _themeService.GetAvailableThemes("Ramza");
        }

        public void ApplyFromConfiguration(Config config)
        {
            var ch1Theme = config.GetStoryCharacterTheme("RamzaChapter1") ?? "original";
            ApplyTheme(ch1Theme);
        }

        #region IMultiChapterCharacterHandler

        public void ApplyPerChapterThemes(Dictionary<string, string> chapterThemes)
        {
            var ch1Theme = chapterThemes.GetValueOrDefault("RamzaChapter1", "original");
            var ch23Theme = chapterThemes.GetValueOrDefault("RamzaChapter23", "original");
            var ch4Theme = chapterThemes.GetValueOrDefault("RamzaChapter4", "original");

            ModLogger.LogDebug($"Applying per-chapter Ramza themes: Ch1={ch1Theme}, Ch23={ch23Theme}, Ch4={ch4Theme}");

            ApplyTexFiles(ch1Theme, ch23Theme, ch4Theme);

            // Apply NXD palettes for all chapters
            ApplyPerChapterNxdPalettes(ch1Theme, ch23Theme, ch4Theme);
        }

        public string[] GetChapterNames()
        {
            return ChapterNames;
        }

        public string GetChapterTheme(string chapterName)
        {
            return _storyCharacterManager.GetCurrentTheme(chapterName);
        }

        #endregion

        #region TEX File Handling

        /// <summary>
        /// Applies TEX files for chapters that use built-in themes.
        /// TEX files are only used for built-in themes (dark_knight, white_heretic, crimson_blade).
        /// User themes and "original" use NXD palettes only.
        /// </summary>
        private void ApplyTexFiles(string ch1Theme, string ch23Theme, string ch4Theme)
        {
            var hasBuiltInTheme = _builtInPalettes.IsBuiltInTheme(ch1Theme) ||
                                  _builtInPalettes.IsBuiltInTheme(ch23Theme) ||
                                  _builtInPalettes.IsBuiltInTheme(ch4Theme);

            var gameTexPath = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "system", "ffto", "g2d");
            var texSwapper = new RamzaTexSwapper(_modPath, gameTexPath);

            if (hasBuiltInTheme)
            {
                // Only swap tex files for built-in themes
                // For chapters with user themes or "original", pass "original" to remove their tex files
                var ch1TexTheme = _builtInPalettes.IsBuiltInTheme(ch1Theme) ? ch1Theme : "original";
                var ch23TexTheme = _builtInPalettes.IsBuiltInTheme(ch23Theme) ? ch23Theme : "original";
                var ch4TexTheme = _builtInPalettes.IsBuiltInTheme(ch4Theme) ? ch4Theme : "original";

                ModLogger.LogDebug($"Tex themes (built-in only): Ch1={ch1TexTheme}, Ch23={ch23TexTheme}, Ch4={ch4TexTheme}");
                texSwapper.SwapTexFilesPerChapter(ch1TexTheme, ch23TexTheme, ch4TexTheme);
            }
            else
            {
                // No built-in themes - remove all tex files to let NXD palettes take effect
                ModLogger.LogDebug("No built-in themes, removing all Ramza tex files");
                texSwapper.RestoreOriginalTexFiles();
            }
        }

        #endregion

        #region NXD Palette Handling

        /// <summary>
        /// Applies built-in Ramza theme palettes to the charclut.nxd file.
        /// </summary>
        private void ApplyNxdPalettes(string theme)
        {
            try
            {
                // Only update NXD for built-in themes and "original"
                // User themes already have their palette applied when saved via SaveRamzaTheme
                if (!_builtInPalettes.IsBuiltInTheme(theme) && theme != "original")
                {
                    ModLogger.LogDebug($"Skipping NXD update for user theme: {theme}");
                    return;
                }

                // Load config to get each chapter's theme selection
                var config = LoadConfig();

                // Get each chapter's configured theme (fallback to "original" if not set)
                var ch1Theme = config?.GetStoryCharacterTheme("RamzaChapter1") ?? "original";
                var ch23Theme = config?.GetStoryCharacterTheme("RamzaChapter23") ?? "original";
                var ch4Theme = config?.GetStoryCharacterTheme("RamzaChapter4") ?? "original";

                ApplyPerChapterNxdPalettes(ch1Theme, ch23Theme, ch4Theme);
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"Error applying theme to NXD: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies NXD palettes for each chapter based on their individual theme selections.
        /// </summary>
        private void ApplyPerChapterNxdPalettes(string ch1Theme, string ch23Theme, string ch4Theme)
        {
            try
            {
                ModLogger.LogDebug($"Applying per-chapter NXD palettes: Ch1={ch1Theme}, Ch23={ch23Theme}, Ch4={ch4Theme}");

                // Get palettes for each chapter based on its configured theme
                var (chapter1Palette, chapter23Palette, chapter4Palette) =
                    _builtInPalettes.GetChapterPalettes(ch1Theme, ch23Theme, ch4Theme);

                var success = _themeSaver.ApplyAllChaptersClutData(
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
                ModLogger.LogError($"Error applying per-chapter NXD palettes: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies a user-created Ramza theme's palette to the charclut.nxd file.
        /// Loads the saved palette from UserThemes folder and patches the NXD.
        /// </summary>
        public void ApplyUserThemeToNxd(string theme)
        {
            try
            {
                var userThemeService = new UserThemeService(_modPath);

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
                            var clutData = _themeSaver.ConvertPaletteToClutData(paletteData);
                            var success = _themeSaver.ApplyClutData(chapterNum, clutData, _modPath);

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

        #endregion

        #region Helper Methods

        private Config LoadConfig()
        {
            var pathResolver = new SimplePathResolver(_sourcePath, _modPath);
            var configService = new ConfigurationService(pathResolver);
            return configService.LoadConfig();
        }

        /// <summary>
        /// Simple path resolver for loading configuration.
        /// </summary>
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
                // First try User/Mods/paxtrick.fft.colorcustomizer directory
                var reloadedDir = Path.GetDirectoryName(Path.GetDirectoryName(_modPath));
                var userConfigPath = Path.Combine(reloadedDir ?? "", "User", "Mods", "paxtrick.fft.colorcustomizer", "Config.json");

                if (File.Exists(userConfigPath))
                {
                    return userConfigPath;
                }

                // Fall back to mod directory
                return Path.Combine(_modPath, "Config.json");
            }

            public string GetDataPath(string relativePath) => Path.Combine(_modPath, "Data", relativePath);
            public string GetSpritePath(string characterName, string themeName, string spriteFileName) =>
                Path.Combine(_sourcePath, $"sprites_{themeName}", spriteFileName);
            public string GetThemeDirectory(string characterName, string themeName) =>
                Path.Combine(_sourcePath, $"sprites_{themeName}");
            public string GetPreviewImagePath(string characterName, string themeName) =>
                Path.Combine(_modPath, "Resources", "Previews", $"{characterName}_{themeName}.png");

            public string ResolveFirstExistingPath(params string[] candidates)
            {
                foreach (var path in candidates)
                {
                    if (File.Exists(path) || Directory.Exists(path))
                        return path;
                }
                return candidates.Length > 0 ? candidates[0] : string.Empty;
            }

            public IEnumerable<string> GetAvailableThemes(string characterName)
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
