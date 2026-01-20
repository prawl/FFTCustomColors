using System;
using System.Collections.Generic;
using System.IO;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Interfaces;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Theme handler for standard story characters (Orlandeau, Agrias, Cloud, etc.).
    /// These characters use simple sprite file copying for theme application.
    /// </summary>
    public class StandardCharacterThemeHandler : ICharacterThemeHandler
    {
        private readonly string _characterName;
        private readonly string _sourcePath;
        private readonly string _modPath;
        private readonly IThemeService _themeService;
        private readonly StoryCharacterThemeManager _storyCharacterManager;
        private readonly string[] _spriteNames;

        public string CharacterName => _characterName;

        public StandardCharacterThemeHandler(
            string characterName,
            string sourcePath,
            string modPath,
            IThemeService themeService,
            StoryCharacterThemeManager storyCharacterManager,
            string[] spriteNames = null)
        {
            _characterName = characterName;
            _sourcePath = sourcePath;
            _modPath = modPath;
            _themeService = themeService;
            _storyCharacterManager = storyCharacterManager;
            _spriteNames = spriteNames ?? GetDefaultSpriteNames(characterName);
        }

        public string CycleTheme()
        {
            var nextTheme = _themeService.CycleTheme(_characterName);
            _storyCharacterManager.SetCurrentTheme(_characterName, nextTheme);
            ModLogger.Log($"{_characterName} theme: {nextTheme}");
            ApplyTheme(nextTheme);
            return nextTheme;
        }

        public void ApplyTheme(string themeName)
        {
            _themeService.ApplyTheme(_characterName, themeName);
            CopyCharacterSprites(themeName);
        }

        public string GetCurrentTheme()
        {
            return _storyCharacterManager.GetCurrentTheme(_characterName);
        }

        public IEnumerable<string> GetAvailableThemes()
        {
            return _themeService.GetAvailableThemes(_characterName);
        }

        public void ApplyFromConfiguration(Config config)
        {
            var theme = config.GetStoryCharacterTheme(_characterName);
            if (!string.IsNullOrEmpty(theme))
            {
                ApplyTheme(theme);
            }
        }

        private void CopyCharacterSprites(string theme)
        {
            foreach (var spriteName in _spriteNames)
            {
                var sourcePath = ResolveSpritePath(theme, spriteName);

                if (sourcePath != null && File.Exists(sourcePath))
                {
                    var destPath = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", spriteName);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                    File.Copy(sourcePath, destPath, true);
                    ModLogger.LogDebug($"Copied {_characterName} sprite: {spriteName}");
                }
            }
        }

        private string ResolveSpritePath(string theme, string spriteName)
        {
            // Try multiple directory structures for compatibility
            var possiblePaths = new[]
            {
                // Character-specific theme directories (e.g., sprites_agrias_original)
                Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit",
                    $"sprites_{_characterName.ToLowerInvariant()}_{theme}", spriteName),
                // Generic theme directories (e.g., sprites_original)
                Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit",
                    $"sprites_{theme}", spriteName),
                // Direct sprites path
                Path.Combine(_sourcePath, $"sprites_{theme}", spriteName)
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        private static string[] GetDefaultSpriteNames(string character)
        {
            return character switch
            {
                "Orlandeau" => new[] { "battle_oru_spr.bin" },
                "Agrias" => new[] { "battle_agri_spr.bin" },
                "Cloud" => new[] { "battle_cloud_spr.bin" },
                "Mustadio" => new[] { "battle_musu_spr.bin" },
                "Marach" => new[] { "battle_mara_spr.bin" },
                "Beowulf" => new[] { "battle_beio_spr.bin" },
                "Meliadoul" => new[] { "battle_h80_spr.bin" },
                "Rapha" => new[] { "battle_h79_spr.bin" },
                "Reis" => new[] { "battle_reis_spr.bin" },
                _ => Array.Empty<string>()
            };
        }
    }
}
