using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ColorMod.Registry;
using FFTColorMod.Services;

namespace FFTColorMod.Configuration.UI
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
        }

        /// <summary>
        /// Auto-discovers all character enums with StoryCharacter attribute
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
                    EnumType = enumType.Name
                };

                // Get available themes from enum values
                var themes = Enum.GetNames(enumType);
                character.AvailableThemes = themes;

                service.AddCharacter(character);
            }
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
                if (string.IsNullOrEmpty(character.EnumType))
                    continue;

                // Get the enum type
                var enumType = Assembly.GetAssembly(typeof(Config))
                    ?.GetType($"FFTColorMod.Configuration.{character.EnumType}");

                if (enumType == null)
                    continue;

                // Get the property from Config
                var configProperty = typeof(Config).GetProperty(character.Name);
                if (configProperty == null)
                    continue;

                result[character.Name] = new StoryCharacterConfig
                {
                    Name = character.Name,
                    EnumType = enumType,
                    GetValue = () => configProperty.GetValue(config),
                    SetValue = (v) => configProperty.SetValue(config, v),
                    PreviewName = character.Name
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
                // Get the "original" enum value for this character's enum type
                var originalValue = Enum.Parse(character.EnumType, "original");
                character.SetValue(originalValue);
            }
        }
    }
}