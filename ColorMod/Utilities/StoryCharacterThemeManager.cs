using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Utilities
{
    /// <summary>
    /// Generic theme manager for story characters using string-based themes
    /// </summary>
    public class StoryCharacterThemeManager
    {
        private readonly Dictionary<string, string> _currentThemes = new();
        private readonly Dictionary<string, List<string>> _availableThemes = new();
        private readonly string _dataPath;

        public StoryCharacterThemeManager(string modPath = null)
        {
            // Determine path to Data directory
            if (string.IsNullOrEmpty(modPath))
            {
                _dataPath = Path.Combine(@"C:\Users\ptyRa\Dev\FFT_Color_Mod\ColorMod", "Data");
            }
            else
            {
                _dataPath = Path.Combine(modPath, "Data");
            }

            LoadStoryCharacterThemes();
        }

        private void LoadStoryCharacterThemes()
        {
            try
            {
                var jsonPath = Path.Combine(_dataPath, "StoryCharacters.json");

                if (!File.Exists(jsonPath))
                {
                    ModLogger.LogWarning($"StoryCharacters.json not found at: {jsonPath}");
                    return;
                }

                var jsonContent = File.ReadAllText(jsonPath);
                var document = JsonDocument.Parse(jsonContent);
                var root = document.RootElement;

                if (root.TryGetProperty("characters", out var charactersArray))
                {
                    foreach (var character in charactersArray.EnumerateArray())
                    {
                        var name = character.GetProperty("name").GetString() ?? "";
                        var defaultTheme = character.GetProperty("defaultTheme").GetString() ?? "original";

                        _currentThemes[name] = defaultTheme;

                        if (character.TryGetProperty("availableThemes", out var themes))
                        {
                            _availableThemes[name] = new List<string>();
                            foreach (var theme in themes.EnumerateArray())
                            {
                                _availableThemes[name].Add(theme.GetString() ?? "original");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"Failed to load StoryCharacters.json: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the current theme for a character
        /// </summary>
        public string GetCurrentTheme(string characterName)
        {
            // First check if we have a theme loaded from JSON
            if (_currentThemes.ContainsKey(characterName))
            {
                return _currentThemes[characterName];
            }

            // Fallback: check old registry for backward compatibility with tests
            var registeredCharacter = ColorMod.Registry.StoryCharacterRegistry.GetCharacter(characterName);

            if (registeredCharacter != null)
            {
                return registeredCharacter.DefaultTheme ?? "original";
            }

            // Default fallback
            return "original";
        }

        /// <summary>
        /// Set the current theme for a character
        /// </summary>
        public void SetCurrentTheme(string characterName, string theme)
        {
            bool isValidTheme = false;

            // First check themes loaded from JSON
            if (_availableThemes.ContainsKey(characterName) &&
                _availableThemes[characterName].Contains(theme))
            {
                isValidTheme = true;
            }
            else
            {
                // Fallback: check old registry enum for backward compatibility
                var registeredCharacter = ColorMod.Registry.StoryCharacterRegistry.GetCharacter(characterName);
                if (registeredCharacter?.EnumType != null && registeredCharacter.EnumType.IsEnum)
                {
                    var availableThemes = Enum.GetNames(registeredCharacter.EnumType);
                    isValidTheme = availableThemes.Contains(theme);
                }
            }

            if (isValidTheme)
            {
                _currentThemes[characterName] = theme;
            }
            else
            {
                ModLogger.LogWarning($"Theme '{theme}' not available for character '{characterName}'");
            }
        }

        /// <summary>
        /// Cycle to the next theme for a character
        /// </summary>
        public string CycleTheme(string characterName)
        {
            List<string> themes;

            // First try to get themes from JSON data
            if (_availableThemes.ContainsKey(characterName) && _availableThemes[characterName].Any())
            {
                themes = _availableThemes[characterName];
            }
            else
            {
                // Fallback: get themes from old registry enum for backward compatibility
                var registeredCharacter = ColorMod.Registry.StoryCharacterRegistry.GetCharacter(characterName);
                if (registeredCharacter?.EnumType != null && registeredCharacter.EnumType.IsEnum)
                {
                    themes = Enum.GetNames(registeredCharacter.EnumType).ToList();
                }
                else
                {
                    return "original";
                }
            }

            var currentTheme = GetCurrentTheme(characterName);
            var currentIndex = themes.IndexOf(currentTheme);

            if (currentIndex == -1)
            {
                currentIndex = 0;
            }
            else
            {
                currentIndex = (currentIndex + 1) % themes.Count;
            }

            var newTheme = themes[currentIndex];
            _currentThemes[characterName] = newTheme;

            return newTheme;
        }

        /// <summary>
        /// Get available themes for a character
        /// </summary>
        public List<string> GetAvailableThemes(string characterName)
        {
            return _availableThemes.GetValueOrDefault(characterName, new List<string> { "original" });
        }

        /// <summary>
        /// Check if a character has a specific theme available
        /// </summary>
        public bool HasTheme(string characterName, string theme)
        {
            return _availableThemes.ContainsKey(characterName) &&
                   _availableThemes[characterName].Contains(theme);
        }

        /// <summary>
        /// Reset all characters to their default theme
        /// </summary>
        public void ResetAllToDefault()
        {
            foreach (var characterName in _currentThemes.Keys.ToList())
            {
                _currentThemes[characterName] = "original";
            }
        }
    }

}
