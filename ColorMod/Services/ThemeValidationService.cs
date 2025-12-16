using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FFTColorMod.Configuration;
using FFTColorMod.Utilities;

namespace FFTColorMod.Services
{
    /// <summary>
    /// Validates that theme files exist for configured story characters
    /// </summary>
    public class ThemeValidationService
    {
        private readonly string _unitPath;
        private readonly CharacterDefinitionService _characterService;
        private readonly List<ValidationResult> _validationResults;

        public class ValidationResult
        {
            public string CharacterName { get; set; }
            public string ThemeName { get; set; }
            public string SpriteName { get; set; }
            public string ExpectedPath { get; set; }
            public bool Exists { get; set; }
            public string Message { get; set; }
        }

        public ThemeValidationService(string modPath, CharacterDefinitionService characterService = null)
        {
            _unitPath = Path.Combine(modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            _characterService = characterService ?? CharacterServiceSingleton.Instance;
            _validationResults = new List<ValidationResult>();
        }

        /// <summary>
        /// Validate all configured themes for a specific configuration
        /// </summary>
        public List<ValidationResult> ValidateConfiguration(Config config)
        {
            _validationResults.Clear();

            // Validate each story character
            foreach (var character in _characterService.GetAllCharacters())
            {
                if (character.SpriteNames?.Length > 0)
                {
                    ValidateCharacterTheme(character, config);
                }
            }

            return _validationResults;
        }

        /// <summary>
        /// Validate a specific character's theme
        /// </summary>
        private void ValidateCharacterTheme(CharacterDefinition character, Config config)
        {
            // Get the current theme for this character
            var theme = config.GetStoryCharacterTheme(character.Name);
            if (theme == null) return;

            var themeName = theme.ToString().ToLower();

            // Skip validation for "original" theme
            if (themeName == "original")
            {
                foreach (var spriteName in character.SpriteNames)
                {
                    _validationResults.Add(new ValidationResult
                    {
                        CharacterName = character.Name,
                        ThemeName = themeName,
                        SpriteName = spriteName,
                        ExpectedPath = "Original (no custom theme)",
                        Exists = true,
                        Message = "Using original sprite"
                    });
                }
                return;
            }

            // Check theme files for each sprite
            foreach (var spriteName in character.SpriteNames)
            {
                ValidateSpriteTheme(character.Name, spriteName, themeName);
            }
        }

        /// <summary>
        /// Validate a specific sprite theme file exists
        /// </summary>
        private void ValidateSpriteTheme(string characterName, string spriteName, string themeName)
        {
            // Build expected paths (checking both directory and flat structure)
            var themeDir = $"sprites_{characterName.ToLower()}_{themeName}";
            var spriteFileName = $"battle_{spriteName}_spr.bin";

            // Directory-based structure
            var dirPath = Path.Combine(_unitPath, themeDir, spriteFileName);

            // Flat file structure
            var flatPath = Path.Combine(_unitPath, $"battle_{spriteName}_{themeName}_spr.bin");

            bool exists = File.Exists(dirPath) || File.Exists(flatPath);

            _validationResults.Add(new ValidationResult
            {
                CharacterName = characterName,
                ThemeName = themeName,
                SpriteName = spriteName,
                ExpectedPath = exists ? (File.Exists(dirPath) ? dirPath : flatPath) : dirPath,
                Exists = exists,
                Message = exists ? "Theme file found" : "WARNING: Theme file missing"
            });
        }

        /// <summary>
        /// Get available themes for a character by scanning the filesystem
        /// </summary>
        public List<string> GetAvailableThemes(string characterName)
        {
            var themes = new HashSet<string> { "original" }; // Always include original

            if (!Directory.Exists(_unitPath))
                return themes.ToList();

            var character = _characterService.GetCharacterByName(characterName);
            if (character?.SpriteNames == null || character.SpriteNames.Length == 0)
                return themes.ToList();

            // Search for theme directories
            var pattern = $"sprites_{characterName.ToLower()}_*";
            var directories = Directory.GetDirectories(_unitPath, pattern);

            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);
                var themeName = dirName.Replace($"sprites_{characterName.ToLower()}_", "");
                if (!string.IsNullOrEmpty(themeName))
                {
                    themes.Add(themeName);
                }
            }

            // Also search for flat files
            foreach (var spriteName in character.SpriteNames)
            {
                var pattern2 = $"battle_{spriteName}_*_spr.bin";
                var files = Directory.GetFiles(_unitPath, pattern2);

                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    // Extract theme name from pattern: battle_{sprite}_{theme}_spr
                    var parts = fileName.Split('_');
                    if (parts.Length >= 3)
                    {
                        // Skip "battle" and the sprite name, take the theme part
                        var themePart = string.Join("_", parts.Skip(2).Take(parts.Length - 3));
                        if (!string.IsNullOrEmpty(themePart) && themePart != spriteName)
                        {
                            themes.Add(themePart);
                        }
                    }
                }
            }

            return themes.OrderBy(t => t).ToList();
        }

        /// <summary>
        /// Log validation results
        /// </summary>
        public void LogValidationResults()
        {
            if (_validationResults.Count == 0)
            {
                ModLogger.Log("No theme validation results to report.");
                return;
            }

            ModLogger.LogSection("Theme Validation Results");

            var missingThemes = _validationResults.Where(r => !r.Exists).ToList();
            var foundThemes = _validationResults.Where(r => r.Exists).ToList();

            if (missingThemes.Any())
            {
                ModLogger.LogWarning($"Found {missingThemes.Count} missing theme files:");
                foreach (var result in missingThemes)
                {
                    ModLogger.LogWarning($"  - {result.CharacterName}: {result.ThemeName} ({result.SpriteName})");
                    ModLogger.LogWarning($"    Expected at: {result.ExpectedPath}");
                }
            }

            ModLogger.LogSuccess($"Validated {foundThemes.Count} theme files successfully");
        }

        /// <summary>
        /// Check if a specific theme is valid for a character
        /// </summary>
        public bool IsThemeValid(string characterName, string themeName)
        {
            if (themeName.ToLower() == "original")
                return true;

            var availableThemes = GetAvailableThemes(characterName);
            return availableThemes.Contains(themeName.ToLower());
        }
    }
}