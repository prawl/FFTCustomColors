using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FFTColorMod.Configuration;

namespace FFTColorMod.Utilities
{
    public class DynamicSpriteLoader
    {
        private readonly string _modPath;
        private readonly string _colorSchemesPath;
        private readonly string _dataPath;
        private readonly ConfigurationManager _configManager;
        private readonly bool _isDevMode;

        // Core dev themes that are always kept for F1/F2 testing
        // Updated to include ALL generic themes since BuildLinked.ps1 now deploys all themes
        private static readonly HashSet<string> CoreDevThemes = new HashSet<string>
        {
            "sprites_original",
            "sprites_corpse_brigade",
            "sprites_lucavi",
            "sprites_northern_sky",
            "sprites_amethyst",
            "sprites_blood_moon",
            "sprites_celestial",
            "sprites_crimson_red",
            "sprites_emerald_dragon",
            "sprites_frost_knight",
            "sprites_golden_templar",
            "sprites_ocean_depths",
            "sprites_phoenix_flame",
            "sprites_rose_gold",
            "sprites_royal_purple",
            "sprites_silver_knight",
            "sprites_southern_sky",
            "sprites_test",
            "sprites_volcanic"
        };

        public DynamicSpriteLoader(string modPath, ConfigurationManager configManager, bool? isDevMode = null)
        {
            _modPath = modPath;
            _configManager = configManager;
            _colorSchemesPath = Path.Combine(_modPath, "ColorSchemes");
            _dataPath = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

            // Auto-detect dev mode if not specified
            _isDevMode = isDevMode ?? DetectDevMode();

            if (_isDevMode)
            {
                Console.WriteLine("[DynamicSpriteLoader] Running in DEV MODE - preserving F1/F2 test themes");
            }
        }

        /// <summary>
        /// Detects if we're in dev mode by checking what themes are in the data directory.
        /// Dev mode = exactly the 5 core dev themes (plus any test/Orlandeau themes).
        /// </summary>
        private bool DetectDevMode()
        {
            if (!Directory.Exists(_dataPath))
                return false;

            var dataThemes = Directory.GetDirectories(_dataPath, "sprites_*")
                .Select(d => Path.GetFileName(d))
                .Where(d => !d.StartsWith("sprites_test_") && !d.StartsWith("sprites_orlandeau_") && !d.StartsWith("sprites_agrias_")) // Exclude test, Orlandeau, and Agrias themes from check
                .ToHashSet();

            // Dev mode if we have exactly the core dev themes (or subset)
            return dataThemes.IsSubsetOf(CoreDevThemes) && dataThemes.Count > 0;
        }

        /// <summary>
        /// Gets whether the loader is running in dev mode.
        /// </summary>
        public bool IsDevMode()
        {
            return _isDevMode;
        }

        /// <summary>
        /// Prepares sprites in the data directory based on current configuration.
        /// In dev mode: Does nothing to preserve F1/F2 testing setup.
        /// In production mode: Copies only the required color schemes from ColorSchemes to data directory.
        /// </summary>
        public void PrepareSpritesForConfig()
        {
            Console.WriteLine($"[DynamicSpriteLoader] PrepareSpritesForConfig called - Dev mode: {_isDevMode}");

            if (_isDevMode)
            {
                Console.WriteLine("[DynamicSpriteLoader] Dev mode detected - preserving existing themes for F1/F2 testing");
                LogSkippedThemes();
                return;
            }

            Console.WriteLine("[DynamicSpriteLoader] Production mode - managing sprites");
            var requiredSchemes = GetRequiredSchemes();
            Console.WriteLine($"[DynamicSpriteLoader] Required schemes: {string.Join(", ", requiredSchemes)}");

            // Copy required schemes from ColorSchemes to data
            CopyRequiredSchemes(requiredSchemes);

            // Remove unused schemes from data (except test themes)
            Console.WriteLine("[DynamicSpriteLoader] Starting cleanup of data directory...");
            CleanupDataDirectory(requiredSchemes);
            Console.WriteLine("[DynamicSpriteLoader] Cleanup complete");
        }

        /// <summary>
        /// Logs which themes are being skipped in dev mode.
        /// </summary>
        private void LogSkippedThemes()
        {
            var config = _configManager.LoadConfig();
            var configuredSchemes = GetRequiredSchemes();

            foreach (var scheme in configuredSchemes)
            {
                if (!CoreDevThemes.Contains($"sprites_{scheme}") && scheme != "original")
                {
                    Console.WriteLine($"[DynamicSpriteLoader] Skipping non-dev theme: {scheme} (use production mode to load)");
                }
            }
        }

        /// <summary>
        /// Gets all unique color schemes required by the current configuration.
        /// </summary>
        public HashSet<string> GetRequiredSchemes()
        {
            var schemes = new HashSet<string>();
            var config = _configManager.LoadConfig();

            // Always need original
            schemes.Add("original");

            // Get all color scheme properties
            var properties = typeof(Config).GetProperties()
                .Where(p => p.PropertyType == typeof(Configuration.ColorScheme));

            foreach (var property in properties)
            {
                var value = property.GetValue(config);
                if (value is Configuration.ColorScheme colorScheme)
                {
                    var schemeName = colorScheme.ToString();
                    if (schemeName != "original")
                    {
                        schemes.Add(schemeName);
                    }
                }
            }

            return schemes;
        }

        /// <summary>
        /// Copies required color schemes from ColorSchemes to data directory.
        /// </summary>
        private void CopyRequiredSchemes(HashSet<string> requiredSchemes)
        {
            foreach (var scheme in requiredSchemes)
            {
                var sourceDir = Path.Combine(_colorSchemesPath, $"sprites_{scheme}");
                var destDir = Path.Combine(_dataPath, $"sprites_{scheme}");

                // If source doesn't exist in ColorSchemes, check if it's already in data
                if (!Directory.Exists(sourceDir))
                {
                    // If it's already in data, keep it
                    if (Directory.Exists(destDir))
                    {
                        Console.WriteLine($"[DynamicSpriteLoader] Keeping existing sprites_{scheme} in data directory");
                        continue;
                    }

                    Console.WriteLine($"[DynamicSpriteLoader] Warning: sprites_{scheme} not found in ColorSchemes");
                    continue;
                }

                // If destination already exists and is up to date, skip
                if (Directory.Exists(destDir))
                {
                    // For now, assume if it exists it's up to date
                    // Could add timestamp checking here if needed
                    Console.WriteLine($"[DynamicSpriteLoader] sprites_{scheme} already exists in data directory");
                    continue;
                }

                // Copy the directory
                CopyDirectory(sourceDir, destDir);
                Console.WriteLine($"[DynamicSpriteLoader] Copied sprites_{scheme} to data directory");
            }
        }

        /// <summary>
        /// Removes color schemes from data directory that are not in the required set.
        /// Preserves test themes (sprites_test_*).
        /// </summary>
        public void CleanupDataDirectory(HashSet<string> requiredSchemes)
        {
            if (!Directory.Exists(_dataPath))
                return;

            var directories = Directory.GetDirectories(_dataPath, "sprites_*");

            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);

                // Always preserve test themes
                if (dirName.StartsWith("sprites_test_"))
                {
                    Console.WriteLine($"[DynamicSpriteLoader] Preserving test theme: {dirName}");
                    continue;
                }

                // Always preserve Orlandeau themes (story character themes)
                if (dirName.StartsWith("sprites_orlandeau_"))
                {
                    Console.WriteLine($"[DynamicSpriteLoader] Preserving Orlandeau theme: {dirName}");
                    continue;
                }

                // In dev mode, also preserve core dev themes for F1/F2 testing
                if (_isDevMode && CoreDevThemes.Contains(dirName))
                {
                    Console.WriteLine($"[DynamicSpriteLoader] Preserving core dev theme: {dirName}");
                    continue;
                }

                // Extract scheme name (remove "sprites_" prefix)
                var schemeName = dirName.Replace("sprites_", "");

                // If not required, remove it
                if (!requiredSchemes.Contains(schemeName))
                {
                    try
                    {
                        Directory.Delete(dir, true);
                        Console.WriteLine($"[DynamicSpriteLoader] Removed unused theme: {dirName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DynamicSpriteLoader] Failed to remove {dirName}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Recursively copies a directory.
        /// </summary>
        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            // Copy all files
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            // Copy all subdirectories
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }
    }
}