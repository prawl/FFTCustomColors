using System;
using System.Linq;
using FFTColorCustomizer.Registry;
using FFTColorCustomizer.Services;
using ColorMod.Registry;

namespace FFTColorCustomizer.Configuration
{
    /// <summary>
    /// Maps sprite file names to character keys and color scheme descriptions
    /// Extracted from Config.GetColorForSprite to separate responsibilities
    /// </summary>
    public class SpriteNameMapper
    {
        private readonly Config _config;
        private readonly GenericCharacterRegistry _genericRegistry;
        private readonly CharacterDefinitionService _characterService;
        private readonly JobClassDefinitionService _jobClassService;

        public SpriteNameMapper(Config config)
        {
            _config = config;
            _genericRegistry = GenericCharacterRegistry.Instance;
            _characterService = CharacterServiceSingleton.Instance;
            _jobClassService = JobClassServiceSingleton.Instance;
        }

        public SpriteNameMapper(CharacterDefinitionService characterService, JobClassDefinitionService jobClassService)
        {
            _config = new Config(); // Default config
            _genericRegistry = GenericCharacterRegistry.Instance;
            _characterService = characterService ?? throw new ArgumentNullException(nameof(characterService));
            _jobClassService = jobClassService ?? throw new ArgumentNullException(nameof(jobClassService));
        }

        /// <summary>
        /// Gets the character key for a given sprite name
        /// </summary>
        public string GetCharacterKeyForSprite(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName))
                return null;

            // Try job class service first (new centralized system)
            var jobClass = _jobClassService.GetJobClassBySpriteName(spriteName);
            if (jobClass != null)
            {
                return jobClass.Name;
            }

            // Fall back to generic registry for backward compatibility
            var genericCharacter = _genericRegistry.GetCharacterBySpriteName(spriteName);
            if (genericCharacter != null)
            {
                return genericCharacter;
            }

            // Try story characters by checking each registered character's sprite names
            foreach (var character in _characterService.GetAllCharacters())
            {
                if (character != null && character.SpriteNames != null)
                {
                    foreach (var spritePattern in character.SpriteNames)
                    {
                        if (spriteName.Contains(spritePattern))
                        {
                            return character.Name;
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
                var colorScheme = _config.GetJobTheme(characterKey);
                // Convert to sprite path format
                if (colorScheme == "original")
                    return "sprites_original";
                return $"sprites_{colorScheme.ToLower()}";
            }

            // Handle story characters
            var storyCharacter = _characterService.GetCharacterByName(characterKey);
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
