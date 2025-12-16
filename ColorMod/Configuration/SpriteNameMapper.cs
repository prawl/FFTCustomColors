using System;
using System.Linq;
using FFTColorMod.Registry;
using ColorMod.Registry;

namespace FFTColorMod.Configuration
{
    /// <summary>
    /// Maps sprite file names to character keys and color scheme descriptions
    /// Extracted from Config.GetColorForSprite to separate responsibilities
    /// </summary>
    public class SpriteNameMapper
    {
        private readonly Config _config;
        private readonly GenericCharacterRegistry _genericRegistry;
        public SpriteNameMapper(Config config)
        {
            _config = config;
            _genericRegistry = GenericCharacterRegistry.Instance;

            // Ensure story characters are discovered
            if (!StoryCharacterRegistry.GetAllCharacterNames().Any())
            {
                StoryCharacterRegistry.AutoDiscoverCharacters();
            }
        }

        /// <summary>
        /// Gets the character key for a given sprite name
        /// </summary>
        public string GetCharacterKeyForSprite(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName))
                return null;

            // Try generic characters first
            var genericCharacter = _genericRegistry.GetCharacterBySpriteName(spriteName);
            if (genericCharacter != null)
            {
                return genericCharacter;
            }

            // Try story characters by checking each registered character's sprite names
            foreach (var characterName in StoryCharacterRegistry.GetAllCharacterNames())
            {
                var character = StoryCharacterRegistry.GetCharacter(characterName);
                if (character != null && character.SpriteNames != null)
                {
                    foreach (var spritePattern in character.SpriteNames)
                    {
                        if (spriteName.Contains(spritePattern))
                        {
                            return characterName;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the color scheme description for a given sprite name
        /// This is what the game uses to find the sprite files
        /// </summary>
        public string GetColorForSprite(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName))
                return "sprites_original";

            var characterKey = GetCharacterKeyForSprite(spriteName);
            if (characterKey == null)
                return "sprites_original";

            // Handle generic characters
            var genericCharacter = _genericRegistry.GetCharacter(characterKey);
            if (genericCharacter != null)
            {
                var colorScheme = _config.GetColorScheme(characterKey);
                // Convert enum to sprite path format
                if (colorScheme == ColorScheme.original)
                    return "sprites_original";
                return $"sprites_{colorScheme.ToString().ToLower()}";
            }

            // Handle story characters
            var storyCharacter = StoryCharacterRegistry.GetCharacter(characterKey);
            if (storyCharacter != null)
            {
                return GetStoryCharacterColor(characterKey, spriteName);
            }

            return "sprites_original";
        }

        private string GetStoryCharacterColor(string characterName, string spriteName)
        {
            // Get the color scheme for the story character directly from config
            var property = typeof(Config).GetProperty(characterName);
            if (property != null)
            {
                var value = property.GetValue(_config);
                if (value != null)
                {
                    var colorScheme = value.ToString().ToLower();
                    if (colorScheme == "original")
                        return "sprites_original";
                    return $"sprites_{characterName.ToLower()}_{colorScheme}";
                }
            }
            return "sprites_original";
        }
    }
}