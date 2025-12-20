using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Core;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Singleton wrapper for CharacterDefinitionService to provide global access
    /// </summary>
    public static class CharacterServiceSingleton
    {
        private static readonly object _lock = new object();
        private static CharacterDefinitionService? _instance;
        private static string? _modPath;

        /// <summary>
        /// Sets the mod path for loading StoryCharacters.json
        /// </summary>
        public static void SetModPath(string modPath)
        {
            lock (_lock)
            {
                _modPath = modPath;
                // Reset instance to force reload with new path
                _instance = null;
            }
        }

        /// <summary>
        /// Gets the singleton instance of CharacterDefinitionService
        /// </summary>
        public static CharacterDefinitionService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = Initialize();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Resets the singleton instance (mainly for testing)
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _instance = null;
            }
        }

        private static CharacterDefinitionService Initialize()
        {
            var service = new CharacterDefinitionService();

            // Try to load from JSON file first
            var jsonPath = FindStoryCharactersJson();
            if (jsonPath != null && File.Exists(jsonPath))
            {
                service.LoadFromJson(jsonPath);
            }

            // If no characters loaded from JSON, auto-discover from attributes
            if (service.GetAllCharacters().Count == 0)
            {
                StoryCharacterRegistry.AutoDiscoverCharacters(service);
            }

            return service;
        }

        private static string? FindStoryCharactersJson()
        {
            var possiblePaths = new List<string>();

            // First check the mod path if set
            if (!string.IsNullOrEmpty(_modPath))
            {
                possiblePaths.Add(Path.Combine(_modPath, ColorModConstants.DataDirectory, ColorModConstants.StoryCharactersFile));
                possiblePaths.Add(Path.Combine(_modPath, "ColorMod", ColorModConstants.DataDirectory, ColorModConstants.StoryCharactersFile));
            }

            // Then try other possible locations for the JSON file
            possiblePaths.AddRange(new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ColorMod", ColorModConstants.DataDirectory, ColorModConstants.StoryCharactersFile),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ColorModConstants.DataDirectory, ColorModConstants.StoryCharactersFile),
                Path.Combine(Directory.GetCurrentDirectory(), "ColorMod", ColorModConstants.DataDirectory, ColorModConstants.StoryCharactersFile),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "ColorMod", ColorModConstants.DataDirectory, ColorModConstants.StoryCharactersFile)
                // Removed DevSourcePath reference - should use mod directory instead
            });

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                    return path;
            }

            return null;
        }
    }
}
