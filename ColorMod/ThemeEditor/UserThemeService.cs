using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.ThemeEditor
{
    /// <summary>
    /// Service for managing user-created themes.
    /// </summary>
    public class UserThemeService
    {
        private readonly string _basePath;
        private readonly string _registryPath;
        private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Original"
        };

        public UserThemeService(string basePath)
        {
            _basePath = basePath;
            _registryPath = basePath != null ? Path.Combine(_basePath, "UserThemes.json") : null;
        }

        public void SaveTheme(string jobName, string themeName, byte[] paletteData)
        {
            // Validate theme name
            if (string.IsNullOrWhiteSpace(themeName))
            {
                throw new ArgumentException("Theme name cannot be empty");
            }

            // Check for reserved names
            if (ReservedNames.Contains(themeName))
            {
                throw new ArgumentException($"'{themeName}' is a reserved theme name");
            }

            // Check for invalid path characters
            var invalidChars = Path.GetInvalidFileNameChars();
            if (themeName.IndexOfAny(invalidChars) >= 0)
            {
                throw new ArgumentException($"Theme name contains invalid characters");
            }

            // Check for duplicate theme name
            var existingThemes = GetUserThemes(jobName);
            if (existingThemes.Contains(themeName))
            {
                throw new InvalidOperationException($"Theme '{themeName}' already exists for {jobName}");
            }

            var themeDir = Path.Combine(_basePath, "UserThemes", jobName, themeName);
            Directory.CreateDirectory(themeDir);

            var palettePath = Path.Combine(themeDir, "palette.bin");
            File.WriteAllBytes(palettePath, paletteData);

            UpdateRegistry(jobName, themeName);
        }

        private void UpdateRegistry(string jobName, string themeName)
        {
            var registry = LoadRegistry();

            if (!registry.ContainsKey(jobName))
            {
                registry[jobName] = new List<string>();
            }

            if (!registry[jobName].Contains(themeName))
            {
                registry[jobName].Add(themeName);
            }

            var json = JsonSerializer.Serialize(registry, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_registryPath, json);
        }

        public List<string> GetUserThemes(string jobName)
        {
            var registry = LoadRegistry();
            if (registry.TryGetValue(jobName, out var themes))
            {
                return themes;
            }
            return new List<string>();
        }

        public Dictionary<string, List<string>> GetAllUserThemes()
        {
            return LoadRegistry();
        }

        public bool IsUserTheme(string jobName, string themeName)
        {
            var themes = GetUserThemes(jobName);
            var result = themes.Contains(themeName);
            ModLogger.Log($"[USER_THEME_SVC] IsUserTheme({jobName}, {themeName}) -> themes=[{string.Join(", ", themes)}], result={result}");
            return result;
        }

        public string? GetUserThemePalettePath(string jobName, string themeName)
        {
            var palettePath = Path.Combine(_basePath, "UserThemes", jobName, themeName, "palette.bin");
            var exists = File.Exists(palettePath);
            ModLogger.Log($"[USER_THEME_SVC] GetUserThemePalettePath({jobName}, {themeName}) -> {palettePath}, exists={exists}");
            return exists ? palettePath : null;
        }

        public void DeleteTheme(string jobName, string themeName)
        {
            // Remove directory
            var themeDir = Path.Combine(_basePath, "UserThemes", jobName, themeName);
            if (Directory.Exists(themeDir))
            {
                Directory.Delete(themeDir, true);
            }

            // Update registry
            var registry = LoadRegistry();
            if (registry.TryGetValue(jobName, out var themes))
            {
                themes.Remove(themeName);
                var json = JsonSerializer.Serialize(registry, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_registryPath, json);
            }
        }

        public byte[]? LoadTheme(string jobName, string themeName)
        {
            var palettePath = Path.Combine(_basePath, "UserThemes", jobName, themeName, "palette.bin");
            if (File.Exists(palettePath))
            {
                return File.ReadAllBytes(palettePath);
            }
            return null;
        }

        #region Multi-Sprite Support

        /// <summary>
        /// Saves a theme for a multi-sprite character, creating palette files for each sprite.
        /// </summary>
        public void SaveThemeForMultiSprite(string jobName, string themeName, byte[] paletteData, string[] spriteNames)
        {
            // Validate theme name
            if (string.IsNullOrWhiteSpace(themeName))
            {
                throw new ArgumentException("Theme name cannot be empty");
            }

            // Check for reserved names
            if (ReservedNames.Contains(themeName))
            {
                throw new ArgumentException($"'{themeName}' is a reserved theme name");
            }

            // Check for invalid path characters
            var invalidChars = Path.GetInvalidFileNameChars();
            if (themeName.IndexOfAny(invalidChars) >= 0)
            {
                throw new ArgumentException($"Theme name contains invalid characters");
            }

            // Check for duplicate theme name
            var existingThemes = GetUserThemes(jobName);
            if (existingThemes.Contains(themeName))
            {
                throw new InvalidOperationException($"Theme '{themeName}' already exists for {jobName}");
            }

            var themeDir = Path.Combine(_basePath, "UserThemes", jobName, themeName);
            Directory.CreateDirectory(themeDir);

            // Save primary palette.bin (for backward compatibility)
            var palettePath = Path.Combine(themeDir, "palette.bin");
            File.WriteAllBytes(palettePath, paletteData);

            // Save palette for each sprite file
            foreach (var spriteName in spriteNames)
            {
                var spriteBaseName = Path.GetFileNameWithoutExtension(spriteName);
                var spritePalettePath = Path.Combine(themeDir, $"{spriteBaseName}_palette.bin");
                File.WriteAllBytes(spritePalettePath, paletteData);
            }

            UpdateRegistry(jobName, themeName);
        }

        /// <summary>
        /// Gets palette paths for all sprites of a multi-sprite theme.
        /// </summary>
        public string[]? GetUserThemePalettePaths(string jobName, string themeName, string[] spriteNames)
        {
            var themeDir = Path.Combine(_basePath, "UserThemes", jobName, themeName);
            if (!Directory.Exists(themeDir))
                return null;

            var paths = new List<string>();
            foreach (var spriteName in spriteNames)
            {
                var spriteBaseName = Path.GetFileNameWithoutExtension(spriteName);
                var spritePalettePath = Path.Combine(themeDir, $"{spriteBaseName}_palette.bin");
                if (File.Exists(spritePalettePath))
                {
                    paths.Add(spritePalettePath);
                }
            }

            return paths.Count == spriteNames.Length ? paths.ToArray() : null;
        }

        /// <summary>
        /// Loads palette data for all sprites of a multi-sprite theme.
        /// </summary>
        public byte[][]? LoadThemeForMultiSprite(string jobName, string themeName, string[] spriteNames)
        {
            var paths = GetUserThemePalettePaths(jobName, themeName, spriteNames);
            if (paths == null)
                return null;

            var palettes = new byte[paths.Length][];
            for (int i = 0; i < paths.Length; i++)
            {
                palettes[i] = File.ReadAllBytes(paths[i]);
            }

            return palettes;
        }

        #endregion

        private Dictionary<string, List<string>> LoadRegistry()
        {
            ModLogger.Log($"[USER_THEME_SVC] LoadRegistry from: {_registryPath}");

            if (_registryPath == null || !File.Exists(_registryPath))
            {
                ModLogger.Log($"[USER_THEME_SVC] Registry not found or path is null");
                return new Dictionary<string, List<string>>();
            }

            var json = File.ReadAllText(_registryPath);
            var registry = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json)
                   ?? new Dictionary<string, List<string>>();

            ModLogger.Log($"[USER_THEME_SVC] Loaded registry with {registry.Count} jobs");
            foreach (var kvp in registry)
            {
                ModLogger.Log($"[USER_THEME_SVC]   {kvp.Key}: [{string.Join(", ", kvp.Value)}]");
            }

            return registry;
        }
    }
}
