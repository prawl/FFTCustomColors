using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FFTColorCustomizer.Services;
using Newtonsoft.Json;

namespace FFTColorCustomizer.Configuration
{
    /// <summary>
    /// Dynamic configuration system for story characters that doesn't require
    /// modifying Config.cs when adding new characters
    /// </summary>
    public class DynamicStoryCharacterConfig
    {
        private readonly Dictionary<string, object> _characterThemes = new Dictionary<string, object>();
        private readonly CharacterDefinitionService _characterService;

        public DynamicStoryCharacterConfig()
        {
            _characterService = CharacterServiceSingleton.Instance;
            InitializeFromService();
        }

        public DynamicStoryCharacterConfig(CharacterDefinitionService characterService)
        {
            _characterService = characterService;
            InitializeFromService();
        }

        /// <summary>
        /// Initialize character themes from the CharacterDefinitionService
        /// </summary>
        private void InitializeFromService()
        {
            foreach (var character in _characterService.GetAllCharacters())
            {
                if (!string.IsNullOrEmpty(character.EnumType))
                {
                    // Get the enum type
                    var enumType = FindEnumType(character.EnumType);
                    if (enumType != null)
                    {
                        // Set default value to "original"
                        var defaultValue = Enum.Parse(enumType, character.DefaultTheme ?? "original");
                        _characterThemes[character.Name] = defaultValue;
                    }
                }
            }
        }

        /// <summary>
        /// Get the theme for a character
        /// </summary>
        public T GetTheme<T>(string characterName) where T : Enum
        {
            if (_characterThemes.TryGetValue(characterName, out var theme))
            {
                return (T)theme;
            }

            // Return default (original) if not found
            return (T)Enum.Parse(typeof(T), "original");
        }

        /// <summary>
        /// Set the theme for a character
        /// </summary>
        public void SetTheme<T>(string characterName, T theme) where T : Enum
        {
            _characterThemes[characterName] = theme;
        }

        /// <summary>
        /// Get theme as object (for reflection-based access)
        /// </summary>
        public object GetThemeObject(string characterName)
        {
            return _characterThemes.TryGetValue(characterName, out var theme) ? theme : null;
        }

        /// <summary>
        /// Set theme as object (for reflection-based access)
        /// </summary>
        public void SetThemeObject(string characterName, object theme)
        {
            _characterThemes[characterName] = theme;
        }

        /// <summary>
        /// Get all character themes for serialization
        /// </summary>
        public Dictionary<string, string> ToSerializableDictionary()
        {
            var result = new Dictionary<string, string>();
            foreach (var kvp in _characterThemes)
            {
                result[kvp.Key] = kvp.Value.ToString();
            }
            return result;
        }

        /// <summary>
        /// Load themes from a dictionary (for deserialization)
        /// </summary>
        public void FromSerializableDictionary(Dictionary<string, string> themes)
        {
            foreach (var kvp in themes)
            {
                var character = _characterService.GetCharacterByName(kvp.Key);
                if (character != null && !string.IsNullOrEmpty(character.EnumType))
                {
                    var enumType = FindEnumType(character.EnumType);
                    if (enumType != null)
                    {
                        try
                        {
                            var enumValue = Enum.Parse(enumType, kvp.Value);
                            _characterThemes[kvp.Key] = enumValue;
                        }
                        catch
                        {
                            // If parsing fails, use default
                            var defaultValue = Enum.Parse(enumType, character.DefaultTheme ?? "original");
                            _characterThemes[kvp.Key] = defaultValue;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Find an enum type by name using reflection
        /// </summary>
        private Type FindEnumType(string enumTypeName)
        {
            // First try in the Configuration namespace
            var fullTypeName = $"FFTColorMod.Configuration.{enumTypeName}";
            var type = Type.GetType(fullTypeName);

            if (type != null && type.IsEnum)
                return type;

            // If not found, search all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetTypes()
                    .FirstOrDefault(t => t.Name == enumTypeName && t.IsEnum);

                if (type != null)
                    return type;
            }

            return null;
        }

        /// <summary>
        /// Check if a character exists in the configuration
        /// </summary>
        public bool HasCharacter(string characterName)
        {
            return _characterThemes.ContainsKey(characterName);
        }

        /// <summary>
        /// Get all configured character names
        /// </summary>
        public IEnumerable<string> GetCharacterNames()
        {
            return _characterThemes.Keys;
        }
    }
}
