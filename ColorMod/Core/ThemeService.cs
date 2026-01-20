using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Interfaces;
using static FFTColorCustomizer.Core.ColorModConstants;

namespace FFTColorCustomizer.Core
{
    /// <summary>
    /// Service for managing themes and their application
    /// </summary>
    public class ThemeService : IThemeService
    {
        private readonly IPathResolver _pathResolver;
        private readonly IConfigurationService _configurationService;
        private readonly Dictionary<string, string> _currentThemes;
        private readonly List<string> _availableThemes;

        public ThemeService(IPathResolver pathResolver, IConfigurationService configurationService)
        {
            _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _currentThemes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _availableThemes = LoadAvailableThemes();
        }

        /// <inheritdoc />
        public void ApplyTheme(string characterName, string themeName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
                throw new ArgumentException("Character name cannot be empty", nameof(characterName));

            if (string.IsNullOrWhiteSpace(themeName))
                throw new ArgumentException("Theme name cannot be empty", nameof(themeName));

            _currentThemes[characterName] = themeName;
        }

        /// <inheritdoc />
        public string CycleTheme(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
                throw new ArgumentException("Character name cannot be empty", nameof(characterName));

            var currentTheme = GetCurrentTheme(characterName);
            var themes = GetAvailableThemes(characterName).ToList();

            if (themes.Count == 0)
                return "original";

            var currentIndex = themes.IndexOf(currentTheme);
            var nextIndex = (currentIndex + 1) % themes.Count;
            var nextTheme = themes[nextIndex];

            ApplyTheme(characterName, nextTheme);
            return nextTheme;
        }

        /// <inheritdoc />
        public IEnumerable<string> GetAvailableThemes(string characterName)
        {
            // For now, return all available themes for any character
            // This can be enhanced to provide character-specific themes
            return _availableThemes;
        }

        /// <inheritdoc />
        public string GetCurrentTheme(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
                throw new ArgumentException("Character name cannot be empty", nameof(characterName));

            return _currentThemes.TryGetValue(characterName, out var theme) ? theme : DefaultTheme;
        }

        /// <inheritdoc />
        public void ApplyConfigurationThemes(Config config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            // Apply job themes
            foreach (var jobKey in config.GetAllJobKeys())
            {
                var themeName = config.GetJobTheme(jobKey);
                if (!string.IsNullOrWhiteSpace(themeName))
                {
                    var characterName = ExtractCharacterName(jobKey);
                    if (!string.IsNullOrWhiteSpace(characterName))
                    {
                        ApplyTheme(characterName, themeName);
                    }
                }
            }

            // Apply story character themes
            foreach (var character in config.GetAllStoryCharacters())
            {
                var themeName = config.GetStoryCharacterTheme(character);
                if (!string.IsNullOrWhiteSpace(themeName))
                {
                    ApplyTheme(character, themeName);
                }
            }
        }

        private List<string> LoadAvailableThemes()
        {
            var themes = new List<string> { DefaultTheme };

            try
            {
                // Look for theme directories
                var dataPath = _pathResolver.GetDataPath("");
                var parentPath = Directory.GetParent(dataPath)?.FullName;

                if (!string.IsNullOrEmpty(parentPath))
                {
                    var spritesDirs = Directory.GetDirectories(parentPath, $"{SpritesPrefix}*");
                    foreach (var dir in spritesDirs)
                    {
                        var themeName = Path.GetFileName(dir).Replace(SpritesPrefix, "");
                        if (!string.IsNullOrWhiteSpace(themeName) && !themes.Contains(themeName))
                        {
                            themes.Add(themeName);
                        }
                    }
                }

                // Add some common themes if they're not already in the list
                var commonThemes = new[] { "corpse_brigade", "lucavi", "northern_sky", "southern_sky" };
                foreach (var theme in commonThemes)
                {
                    if (!themes.Contains(theme))
                    {
                        themes.Add(theme);
                    }
                }
            }
            catch
            {
                // If we can't load themes, just return the default list
            }

            return themes;
        }

        private string ExtractCharacterName(string propertyName)
        {
            // Extract character name from property name
            // e.g., "Knight_Male" -> "Knight"
            // e.g., "Agrias" -> "Agrias"

            if (string.IsNullOrWhiteSpace(propertyName))
                return null;

            // Handle story characters (single name)
            if (!propertyName.Contains("_"))
                return propertyName;

            // Handle job classes (Name_Gender format)
            var parts = propertyName.Split('_');
            return parts.Length > 0 ? parts[0] : propertyName;
        }
    }
}
