using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FFTColorCustomizer.Interfaces;
using FFTColorCustomizer.Services;

namespace FFTColorCustomizer.Core
{
    /// <summary>
    /// Implementation of IPathResolver for centralized path management
    /// </summary>
    public class PathResolver : IPathResolver
    {
        public string ModRootPath { get; }
        public string SourcePath { get; }
        public string UserConfigPath { get; }
        private readonly CharacterDefinitionService _characterService;

        public PathResolver(string modRootPath, string sourcePath, string userConfigPath, CharacterDefinitionService? characterService = null)
        {
            ModRootPath = modRootPath ?? throw new ArgumentNullException(nameof(modRootPath));
            SourcePath = sourcePath ?? throw new ArgumentNullException(nameof(sourcePath));
            UserConfigPath = userConfigPath ?? throw new ArgumentNullException(nameof(userConfigPath));
            _characterService = characterService ?? LoadCharacterService();
        }

        private CharacterDefinitionService LoadCharacterService()
        {
            var service = new CharacterDefinitionService();

            // Try to load from JSON file
            var jsonPath = GetDataPath(ColorModConstants.StoryCharactersFile);
            if (File.Exists(jsonPath))
            {
                service.LoadFromJson(jsonPath);
            }

            return service;
        }

        public string GetDataPath(string relativePath)
        {
            return Path.Combine(ModRootPath, ColorModConstants.DataDirectory, relativePath);
        }

        public string GetSpritePath(string characterName, string themeName, string spriteFileName)
        {
            return Path.Combine(SourcePath, ColorModConstants.FFTIVCPath, "data", ColorModConstants.EnhancedPath,
                ColorModConstants.FFTPackPath, ColorModConstants.UnitPath, themeName, spriteFileName);
        }

        public string GetThemeDirectory(string characterName, string themeName)
        {
            return Path.Combine(SourcePath, ColorModConstants.FFTIVCPath, "data", ColorModConstants.EnhancedPath,
                ColorModConstants.FFTPackPath, ColorModConstants.UnitPath, themeName);
        }

        public string GetConfigPath()
        {
            return Path.Combine(UserConfigPath, ColorModConstants.ConfigFileName);
        }

        public string GetPreviewImagePath(string characterName, string themeName)
        {
            var characterLower = characterName.ToLowerInvariant();
            var fileName = $"{ColorModConstants.PreviewPrefix}{characterLower}_{themeName}.png";
            return Path.Combine(SourcePath, ColorModConstants.FFTIVCPath, "data", ColorModConstants.EnhancedPath,
                ColorModConstants.FFTPackPath, ColorModConstants.UnitPath, themeName, fileName);
        }

        public string ResolveFirstExistingPath(params string[] candidates)
        {
            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
            return null;
        }

        public IEnumerable<string> GetAvailableThemes(string characterName)
        {
            var unitPath = Path.Combine(SourcePath, ColorModConstants.FFTIVCPath, "data",
                ColorModConstants.EnhancedPath, ColorModConstants.FFTPackPath, ColorModConstants.UnitPath);

            if (!Directory.Exists(unitPath))
            {
                return Enumerable.Empty<string>();
            }

            var themes = new List<string>();
            var directories = Directory.GetDirectories(unitPath);

            foreach (var dir in directories)
            {
                var themeName = Path.GetFileName(dir);
                // Check if this theme has sprites for the character
                var hasCharacterSprites = HasCharacterSprites(dir, characterName);
                if (hasCharacterSprites)
                {
                    themes.Add(themeName);
                }
            }

            return themes;
        }

        private bool HasCharacterSprites(string themeDirectory, string characterName)
        {
            // Map character names to sprite prefixes
            var spritePrefix = GetSpritePrefixForCharacter(characterName);
            if (string.IsNullOrEmpty(spritePrefix))
            {
                return false;
            }

            // Check if any files match the character's sprite pattern
            var files = Directory.GetFiles(themeDirectory, $"{spritePrefix}*{ColorModConstants.BitmapExtension}");
            return files.Length > 0;
        }

        private string GetSpritePrefixForCharacter(string characterName)
        {
            // Get character definition from service
            var character = _characterService?.GetCharacterByName(characterName);
            if (character != null && character.SpriteNames != null && character.SpriteNames.Length > 0)
            {
                // Use the first sprite name and add battle_ prefix
                return $"{ColorModConstants.BattlePrefix}{character.SpriteNames[0]}";
            }

            // Fallback for job classes if needed
            // Could also load from JobClasses.json
            return null;
        }
    }
}
