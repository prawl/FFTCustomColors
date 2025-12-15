using System;
using System.Collections.Generic;

namespace ColorMod.Registry
{
    public class StoryCharacterThemeManager
    {
        private readonly Dictionary<string, object> _currentThemes = new();

        public T GetTheme<T>(string characterName) where T : Enum
        {
            if (!_currentThemes.ContainsKey(characterName))
            {
                // Get default theme from registry
                if (StoryCharacterRegistry.HasCharacter(characterName))
                {
                    var character = StoryCharacterRegistry.GetCharacter(characterName);
                    var defaultTheme = character.DefaultTheme;

                    // Parse the default theme string to the enum
                    if (Enum.TryParse(character.EnumType, defaultTheme, out var parsedTheme))
                    {
                        _currentThemes[characterName] = parsedTheme;
                    }
                    else
                    {
                        // If parsing fails, use first enum value
                        var values = Enum.GetValues(character.EnumType);
                        _currentThemes[characterName] = values.GetValue(0);
                    }
                }
                else
                {
                    // Character not registered, use first enum value
                    var values = Enum.GetValues(typeof(T));
                    _currentThemes[characterName] = values.GetValue(0);
                }
            }

            return (T)_currentThemes[characterName];
        }

        public T CycleTheme<T>(string characterName) where T : Enum
        {
            var current = GetTheme<T>(characterName);
            var values = Enum.GetValues(typeof(T));
            var currentIndex = Array.IndexOf(values, current);
            var nextIndex = (currentIndex + 1) % values.Length;

            _currentThemes[characterName] = values.GetValue(nextIndex);
            return (T)_currentThemes[characterName];
        }

        public void SetTheme<T>(string characterName, T theme) where T : Enum
        {
            _currentThemes[characterName] = theme;
        }

        public string GetThemeString(string characterName)
        {
            if (!_currentThemes.ContainsKey(characterName))
            {
                // Initialize with default theme if not set
                if (StoryCharacterRegistry.HasCharacter(characterName))
                {
                    var character = StoryCharacterRegistry.GetCharacter(characterName);
                    return character.DefaultTheme;
                }
                return "original";
            }

            return _currentThemes[characterName].ToString();
        }

        public void CycleThemeGeneric(string characterName)
        {
            if (!StoryCharacterRegistry.HasCharacter(characterName))
                return;

            var character = StoryCharacterRegistry.GetCharacter(characterName);
            var enumType = character.EnumType;

            if (!_currentThemes.ContainsKey(characterName))
            {
                // Initialize with default
                if (Enum.TryParse(enumType, character.DefaultTheme, out var defaultTheme))
                {
                    _currentThemes[characterName] = defaultTheme;
                }
                else
                {
                    var values = Enum.GetValues(enumType);
                    _currentThemes[characterName] = values.GetValue(0);
                }
            }

            var current = _currentThemes[characterName];
            var allValues = Enum.GetValues(enumType);
            var currentIndex = Array.IndexOf(allValues, current);
            var nextIndex = (currentIndex + 1) % allValues.Length;

            _currentThemes[characterName] = allValues.GetValue(nextIndex);
        }
    }
}