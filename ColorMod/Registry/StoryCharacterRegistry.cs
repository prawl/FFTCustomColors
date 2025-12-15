using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ColorMod.Registry
{
    public static class StoryCharacterRegistry
    {
        private static readonly ConcurrentDictionary<string, StoryCharacterDefinition> _characters = new();

        public static void Register(StoryCharacterDefinition definition)
        {
            _characters.TryAdd(definition.Name, definition);
        }

        public static bool HasCharacter(string name)
        {
            return _characters.ContainsKey(name);
        }

        public static StoryCharacterDefinition GetCharacter(string name)
        {
            _characters.TryGetValue(name, out var character);
            return character;
        }

        public static void Clear()
        {
            _characters.Clear();
        }

        public static IEnumerable<string> GetAllCharacterNames()
        {
            return _characters.Keys;
        }

        public static void AutoDiscoverCharacters()
        {
            // Search all loaded assemblies for enums with the attribute
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                try
                {
                    var enumsWithAttribute = assembly.GetTypes()
                        .Where(t => t.IsEnum && t.GetCustomAttribute<StoryCharacterAttribute>() != null);

                    foreach (var enumType in enumsWithAttribute)
                    {
                        var attribute = enumType.GetCustomAttribute<StoryCharacterAttribute>();
                        var characterName = enumType.Name.Replace("ColorScheme", "");

                        var definition = new StoryCharacterDefinition
                        {
                            Name = characterName,
                            EnumType = enumType,
                            SpriteNames = attribute.SpriteNames,
                            DefaultTheme = attribute.DefaultTheme
                        };

                        _characters.AddOrUpdate(characterName, definition, (key, old) => definition);
                    }
                }
                catch (Exception)
                {
                    // Skip assemblies that can't be reflected
                    continue;
                }
            }
        }
    }
}