using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ColorMod.Registry;
using FFTColorCustomizer.Services;

namespace FFTColorCustomizer.Configuration.UI
{
    /// <summary>
    /// Registry that uses CharacterDefinitionService for centralized character management
    /// </summary>
    public static class StoryCharacterRegistry
    {
        public delegate object GetValueDelegate();
        public delegate void SetValueDelegate(object value);

        public class StoryCharacterConfig
        {
            public string Name { get; set; }
            public Type EnumType { get; set; }
            public GetValueDelegate GetValue { get; set; }
            public SetValueDelegate SetValue { get; set; }
            public string PreviewName { get; set; }
            public string[] AvailableThemes { get; set; } = Array.Empty<string>();
        }

        /// <summary>
        /// Auto-discovers all character enums with StoryCharacter attribute
        /// Note: After refactoring, this primarily serves as a fallback when JSON loading fails
        /// </summary>
        public static void AutoDiscoverCharacters(CharacterDefinitionService service)
        {
            var assembly = Assembly.GetAssembly(typeof(Config));
            if (assembly == null) return;

            var storyCharacterTypes = assembly.GetTypes()
                .Where(t => t.IsEnum && t.GetCustomAttribute<StoryCharacterAttribute>() != null);

            foreach (var enumType in storyCharacterTypes)
            {
                var attribute = enumType.GetCustomAttribute<StoryCharacterAttribute>();
                if (attribute == null) continue;

                var character = new CharacterDefinition
                {
                    Name = enumType.Name.Replace("ColorScheme", ""),
                    SpriteNames = attribute.SpriteNames ?? Array.Empty<string>(),
                    DefaultTheme = attribute.DefaultTheme ?? "original",
                    EnumType = "string" // Updated to use string instead of enum
                };

                // Get available themes from enum values
                var themes = Enum.GetNames(enumType);
                character.AvailableThemes = themes;

                service.AddCharacter(character);
            }

            // Note: After refactoring to remove ColorScheme enums, this method will find 0 attributes
            // The primary character loading now happens from StoryCharacters.json via CharacterServiceSingleton
        }

        /// <summary>
        /// Gets story characters from the CharacterDefinitionService
        /// </summary>
        public static Dictionary<string, StoryCharacterConfig> GetStoryCharactersFromService(
            Config config, CharacterDefinitionService service)
        {
            var result = new Dictionary<string, StoryCharacterRegistry.StoryCharacterConfig>();

            foreach (var character in service.GetAllCharacters())
            {
                // Skip characters without names
                if (string.IsNullOrEmpty(character.Name))
                    continue;

                // Get the property from Config (for story characters like Agrias, Cloud, etc.)
                var configProperty = typeof(Config).GetProperty(character.Name);
                if (configProperty == null || configProperty.PropertyType != typeof(string))
                    continue;

                result[character.Name] = new StoryCharacterConfig
                {
                    Name = character.Name,
                    EnumType = typeof(string), // Now using string instead of enum
                    GetValue = () => configProperty.GetValue(config) ?? character.DefaultTheme,
                    SetValue = (v) => configProperty.SetValue(config, v?.ToString() ?? character.DefaultTheme),
                    PreviewName = character.Name,
                    AvailableThemes = character.AvailableThemes
                };
            }

            return result;
        }

        /// <summary>
        /// Gets story characters using the CharacterServiceSingleton (backward compatibility method)
        /// </summary>
        public static Dictionary<string, StoryCharacterConfig> GetStoryCharacters(Config config)
        {
            var service = CharacterServiceSingleton.Instance;
            return GetStoryCharactersFromService(config, service);
        }

        /// <summary>
        /// Resets all story character themes to original
        /// </summary>
        public static void ResetAllStoryCharacters(Config config)
        {
            var characters = GetStoryCharacters(config);
            foreach (var character in characters.Values)
            {
                // Set to "original" theme string
                character.SetValue("original");
            }
        }
    }
}
