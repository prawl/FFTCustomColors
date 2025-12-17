using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Resolves sprite files based on naming conventions, eliminating the need for hardcoded mappings
    /// </summary>
    public class ConventionBasedSpriteResolver
    {
        private readonly string _basePath;

        public ConventionBasedSpriteResolver(string basePath)
        {
            _basePath = basePath;
        }

        /// <summary>
        /// Resolves the sprite file path for a character/theme combination
        /// Follows conventions:
        /// 1. Directory: sprites_[character]_[theme]/battle_[sprite]_spr.bin
        /// 2. Flat file: battle_[sprite]_[theme]_spr.bin
        /// </summary>
        public string? ResolveSpriteTheme(string characterName, string spriteName, string themeName)
        {
            // First try directory-based convention
            var themeDir = $"sprites_{characterName.ToLower()}_{themeName.ToLower()}";
            var dirPath = Path.Combine(_basePath, themeDir, $"battle_{spriteName}_spr.bin");

            if (File.Exists(dirPath))
                return dirPath;

            // Fallback to flat file convention
            var flatPath = Path.Combine(_basePath, $"battle_{spriteName}_{themeName}_spr.bin");

            if (File.Exists(flatPath))
                return flatPath;

            return null;
        }

        /// <summary>
        /// Discovers all available themes for a character by scanning the file system
        /// </summary>
        public List<string> DiscoverAvailableThemes(string characterName, string spriteName)
        {
            var themes = new HashSet<string>();

            // Check for directory-based themes
            var directoryPattern = $"sprites_{characterName.ToLower()}_*";
            if (Directory.Exists(_basePath))
            {
                var directories = Directory.GetDirectories(_basePath, directoryPattern);
                foreach (var dir in directories)
                {
                    var dirName = Path.GetFileName(dir);
                    var match = Regex.Match(dirName, $@"sprites_{characterName.ToLower()}_(.+)");
                    if (match.Success)
                    {
                        themes.Add(match.Groups[1].Value);
                    }
                }

                // Check for flat file themes
                var flatPattern = $"battle_{spriteName}_*_spr.bin";
                var files = Directory.GetFiles(_basePath, flatPattern);
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    var match = Regex.Match(fileName, $@"battle_{spriteName}_(.+)_spr\.bin");
                    if (match.Success)
                    {
                        themes.Add(match.Groups[1].Value);
                    }
                }
            }

            return themes.ToList();
        }

        /// <summary>
        /// Discovers all sprite files for a character across all themes
        /// </summary>
        public Dictionary<string, List<string>> DiscoverCharacterSprites(string characterName)
        {
            var result = new Dictionary<string, List<string>>();

            if (!Directory.Exists(_basePath))
                return result;

            // Check directory-based structure
            var directoryPattern = $"sprites_{characterName.ToLower()}_*";
            var directories = Directory.GetDirectories(_basePath, directoryPattern);

            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);
                var themeMatch = Regex.Match(dirName, $@"sprites_{characterName.ToLower()}_(.+)");
                if (themeMatch.Success)
                {
                    var theme = themeMatch.Groups[1].Value;
                    var spriteFiles = Directory.GetFiles(dir, "battle_*_spr.bin");

                    var sprites = new List<string>();
                    foreach (var file in spriteFiles)
                    {
                        var fileName = Path.GetFileName(file);
                        var spriteMatch = Regex.Match(fileName, @"battle_(.+)_spr\.bin");
                        if (spriteMatch.Success)
                        {
                            sprites.Add(spriteMatch.Groups[1].Value);
                        }
                    }

                    if (sprites.Count > 0)
                        result[theme] = sprites;
                }
            }

            return result;
        }
    }
}
