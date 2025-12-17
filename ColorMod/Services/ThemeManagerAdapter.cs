using System;
using System.IO;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Interfaces;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Adapter that maintains backward compatibility with ThemeManager
    /// while using the new ThemeService internally
    /// </summary>
    public class ThemeManagerAdapter
    {
        private readonly IThemeService _themeService;
        private readonly StoryCharacterThemeManager _storyCharacterManager;
        private readonly string _sourcePath;
        private readonly string _modPath;

        public ThemeManagerAdapter(string sourcePath, string modPath)
        {
            _sourcePath = sourcePath;
            _modPath = modPath;

            // Create services
            var pathResolver = new SimplePathResolver(sourcePath, modPath);
            var configService = new ConfigurationService(pathResolver);
            _themeService = new ThemeService(pathResolver, configService);

            // Keep StoryCharacterThemeManager for backward compatibility
            _storyCharacterManager = new StoryCharacterThemeManager(sourcePath);
        }

        public StoryCharacterThemeManager GetStoryCharacterManager()
        {
            return _storyCharacterManager;
        }

        public void ApplyInitialThemes()
        {
            Console.WriteLine("ThemeManagerAdapter.ApplyInitialThemes() called");
            ApplyInitialOrlandeauTheme();
            ApplyInitialAgriasTheme();
            ApplyInitialCloudTheme();
        }

        public void CycleOrlandeauTheme()
        {
            var nextTheme = _themeService.CycleTheme("Orlandeau");
            _storyCharacterManager.SetCurrentTheme("Orlandeau", nextTheme);
            ModLogger.Log($"Orlandeau theme: {nextTheme}");
            ApplyOrlandeauTheme(nextTheme);
        }

        public void CycleAgriasTheme()
        {
            var nextTheme = _themeService.CycleTheme("Agrias");
            _storyCharacterManager.SetCurrentTheme("Agrias", nextTheme);
            ModLogger.Log($"================================================");
            ModLogger.Log($"    AGRIAS THEME CHANGED TO: {nextTheme}");
            ModLogger.Log($"================================================");
            ApplyAgriasTheme(nextTheme);
        }

        public void CycleCloudTheme()
        {
            var nextTheme = _themeService.CycleTheme("Cloud");
            _storyCharacterManager.SetCurrentTheme("Cloud", nextTheme);
            ModLogger.Log($"Cloud theme: {nextTheme}");
            ApplyCloudTheme(nextTheme);
        }

        private void ApplyInitialOrlandeauTheme()
        {
            var theme = _storyCharacterManager.GetCurrentTheme("Orlandeau");
            Console.WriteLine($"ApplyInitialOrlandeauTheme - theme: {theme}");
            ApplyOrlandeauTheme(theme);
        }

        private void ApplyInitialAgriasTheme()
        {
            var theme = _storyCharacterManager.GetCurrentTheme("Agrias");
            Console.WriteLine($"ApplyInitialAgriasTheme - theme: {theme}");
            ApplyAgriasTheme(theme);
        }

        private void ApplyInitialCloudTheme()
        {
            var theme = _storyCharacterManager.GetCurrentTheme("Cloud");
            Console.WriteLine($"ApplyInitialCloudTheme - theme: {theme}");
            ApplyCloudTheme(theme);
        }

        private void ApplyOrlandeauTheme(string theme)
        {
            _themeService.ApplyTheme("Orlandeau", theme);
            // Copy sprite files as needed - this would use SpriteService
            CopyCharacterSprites("Orlandeau", theme);
        }

        private void ApplyAgriasTheme(string theme)
        {
            _themeService.ApplyTheme("Agrias", theme);
            CopyCharacterSprites("Agrias", theme);
        }

        private void ApplyCloudTheme(string theme)
        {
            _themeService.ApplyTheme("Cloud", theme);
            CopyCharacterSprites("Cloud", theme);
        }

        private void CopyCharacterSprites(string character, string theme)
        {
            // This implementation would use SpriteService
            // For now, copy files directly to maintain compatibility
            var spriteNames = GetSpriteNamesForCharacter(character);

            foreach (var spriteName in spriteNames)
            {
                // Try multiple directory structures for compatibility
                var possiblePaths = new[]
                {
                    // Character-specific theme directories (e.g., sprites_agrias_original)
                    Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit",
                        $"sprites_{character.ToLowerInvariant()}_{theme}", spriteName),
                    // Generic theme directories (e.g., sprites_original)
                    Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit",
                        $"sprites_{theme}", spriteName),
                    // Direct sprites path
                    Path.Combine(_sourcePath, $"sprites_{theme}", spriteName)
                };

                string sourcePath = null;
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        sourcePath = path;
                        break;
                    }
                }

                if (sourcePath != null)
                {
                    var destPath = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", spriteName);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                    File.Copy(sourcePath, destPath, true);
                    ModLogger.Log($"Copied {character} sprite from {sourcePath} to {destPath}");
                }
            }
        }

        private string[] GetSpriteNamesForCharacter(string character)
        {
            return character switch
            {
                "Orlandeau" => new[] { "battle_oru_spr.bin", "battle_oru_out.bin" },
                "Agrias" => new[] { "battle_aguri_spr.bin", "battle_aguri_out.bin" },
                "Cloud" => new[] { "battle_cloud_spr.bin", "battle_cloud_out.bin" },
                _ => Array.Empty<string>()
            };
        }

        // Simple path resolver for the adapter
        private class SimplePathResolver : IPathResolver
        {
            private readonly string _sourcePath;
            private readonly string _modPath;

            public SimplePathResolver(string sourcePath, string modPath)
            {
                _sourcePath = sourcePath;
                _modPath = modPath;
            }

            public string ModRootPath => _modPath;
            public string SourcePath => _sourcePath;
            public string UserConfigPath => _modPath;

            public string GetConfigPath()
            {
                return Path.Combine(_modPath, "Config.json");
            }

            public string GetDataPath(string relativePath)
            {
                return Path.Combine(_modPath, "Data", relativePath);
            }

            public string GetSpritePath(string characterName, string themeName, string spriteFileName)
            {
                return Path.Combine(_sourcePath, $"sprites_{themeName}", spriteFileName);
            }

            public string GetThemeDirectory(string characterName, string themeName)
            {
                return Path.Combine(_sourcePath, $"sprites_{themeName}");
            }

            public string GetPreviewImagePath(string characterName, string themeName)
            {
                return Path.Combine(_modPath, "Resources", "Previews", $"{characterName}_{themeName}.png");
            }

            public string ResolveFirstExistingPath(params string[] candidates)
            {
                foreach (var path in candidates)
                {
                    if (File.Exists(path) || Directory.Exists(path))
                        return path;
                }
                return candidates.Length > 0 ? candidates[0] : string.Empty;
            }

            public System.Collections.Generic.IEnumerable<string> GetAvailableThemes(string characterName)
            {
                var themesPath = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
                if (Directory.Exists(themesPath))
                {
                    var dirs = Directory.GetDirectories(themesPath, "sprites_*");
                    foreach (var dir in dirs)
                    {
                        yield return Path.GetFileName(dir).Replace("sprites_", "");
                    }
                }
            }
        }
    }
}
