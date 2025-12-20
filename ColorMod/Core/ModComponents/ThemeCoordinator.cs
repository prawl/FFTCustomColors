using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.Utilities;
using static FFTColorCustomizer.Core.ColorModConstants;

namespace FFTColorCustomizer.Core.ModComponents
{
    /// <summary>
    /// Coordinates theme and sprite operations
    /// </summary>
    public class ThemeCoordinator
    {
        private readonly string _sourcePath;
        private readonly string _modPath;
        private readonly ColorSchemeCycler _colorCycler;
        private readonly ThemeManager _themeManager;
        private string _currentColorScheme;

        // Job sprite patterns (includes both generic jobs and story characters)
        private readonly string[] _jobSpritePatterns = new[]
        {
            // Generic job classes
            "knight", "archer", "monk", "whitemage", "blackmage",
            "thief", "squire", "chemist", "ninja", "samurai",
            "dragoon", "summoner", "timemage", "geomancer",
            "mystic", "mediator", "dancer", "bard", "mime",
            "calculator", "kuro", "siro", "yumi", "item",
            "mina", "toki", "syou", "samu", "ryu", "fusui",
            "onmyo", "waju", "odori", "gin", "mono", "san",

            // Story characters
            "musu",     // Mustadio
            "aguri",    // Agrias
            "kanba",    // Agrias second sprite
            "oru",      // Orlandeau (correct name, NOT "oran")
            "dily",     // Delita (all chapters)
            "dily2",    // Delita chapter 2
            "dily3",    // Delita chapter 3
            "aruma",    // Alma
            "rafa",     // Rafa (old name)
            "h79",      // Rapha (actual sprite name)
            "mara",     // Malak/Marach
            "cloud",    // Cloud
            "reze",     // Reis human
            "reze_d"    // Reis dragon
        };

        public ThemeCoordinator(string sourcePath, string modPath)
        {
            _sourcePath = sourcePath ?? throw new ArgumentNullException(nameof(sourcePath));
            _modPath = modPath ?? throw new ArgumentNullException(nameof(modPath));

            // Initialize color cycler - use _modPath for deployed environment
            string spritesPath = Path.Combine(_modPath, FFTIVCPath, DataPath, EnhancedPath, FFTPackPath, UnitPath);
            _colorCycler = new ColorSchemeCycler(spritesPath);

            var schemes = _colorCycler.GetAvailableSchemes();
            if (schemes.Count > 0)
            {
                ModLogger.Log($"ThemeCoordinator: Found {schemes.Count} color schemes");
                _currentColorScheme = DefaultTheme;
                _colorCycler.SetCurrentScheme(_currentColorScheme);
            }
            else
            {
                ModLogger.LogWarning($"ThemeCoordinator: No color schemes found in {spritesPath}");
                _currentColorScheme = DefaultTheme;
            }

            // Initialize theme manager
            _themeManager = new ThemeManager(_sourcePath, _modPath);
        }

        /// <summary>
        /// Get the current color scheme
        /// </summary>
        public string GetCurrentColorScheme()
        {
            return _currentColorScheme;
        }

        /// <summary>
        /// Set the color scheme
        /// </summary>
        public void SetColorScheme(string scheme)
        {
            _currentColorScheme = scheme;
            _colorCycler?.SetCurrentScheme(scheme);
            ModLogger.Log($"Color scheme set to: {scheme}");
        }

        /// <summary>
        /// Cycle to the next color scheme
        /// </summary>
        public void CycleColorScheme()
        {
            if (_colorCycler != null)
            {
                _currentColorScheme = _colorCycler.GetNextScheme();
                ModLogger.Log($"Color scheme cycled to: {_currentColorScheme}");
            }
        }

        /// <summary>
        /// Get available color schemes
        /// </summary>
        public IList<string> GetAvailableSchemes()
        {
            return _colorCycler?.GetAvailableSchemes() ?? new List<string> { DefaultTheme };
        }

        /// <summary>
        /// Check if a file is a job sprite
        /// </summary>
        public bool IsJobSprite(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            var lowerFileName = fileName.ToLowerInvariant();

            // Must be a battle sprite file
            if (!lowerFileName.Contains("battle_") || !lowerFileName.EndsWith("_spr.bin"))
                return false;

            // Check if it matches any job sprite pattern
            return _jobSpritePatterns.Any(pattern => lowerFileName.Contains(pattern));
        }

        /// <summary>
        /// Intercept file path for sprite replacement
        /// </summary>
        public string InterceptFilePath(string originalPath)
        {
            if (string.IsNullOrEmpty(originalPath))
                return originalPath;

            var fileName = Path.GetFileName(originalPath);

            // Only intercept job sprites when not using original theme
            if (!IsJobSprite(fileName) || _currentColorScheme == DefaultTheme)
            {
                return originalPath;
            }

            // Build the themed sprite path - use _modPath for deployed environment, not _sourcePath
            var themedDir = Path.Combine(_modPath, FFTIVCPath, DataPath, EnhancedPath,
                FFTPackPath, UnitPath, $"{SpritesPrefix}{_currentColorScheme}");
            var themedPath = Path.Combine(themedDir, fileName);

            // Return themed path if it exists, otherwise original
            if (File.Exists(themedPath))
            {
                ModLogger.Log($"Intercepted: {fileName} -> {_currentColorScheme}");
                return themedPath;
            }

            return originalPath;
        }

        /// <summary>
        /// Initialize themes
        /// </summary>
        public void InitializeThemes()
        {
            if (_themeManager != null)
            {
                _themeManager.ApplyInitialThemes();
                ModLogger.Log("Themes initialized");
            }
        }

        /// <summary>
        /// Get the theme manager
        /// </summary>
        public ThemeManager GetThemeManager()
        {
            return _themeManager;
        }

        /// <summary>
        /// Apply a specific theme
        /// </summary>
        public void ApplyTheme(string characterName, string themeName)
        {
            var storyManager = _themeManager?.GetStoryCharacterManager();
            if (storyManager != null)
            {
                storyManager.SetCurrentTheme(characterName, themeName);
                ModLogger.Log($"Applied {themeName} theme to {characterName}");
            }
        }

        /// <summary>
        /// Cycle theme for a specific character
        /// </summary>
        public string CycleCharacterTheme(string characterName)
        {
            var storyManager = _themeManager?.GetStoryCharacterManager();
            if (storyManager != null)
            {
                var newTheme = storyManager.CycleTheme(characterName);
                ModLogger.Log($"{characterName} theme cycled to: {newTheme}");
                return newTheme;
            }
            return DefaultTheme;
        }
    }
}
