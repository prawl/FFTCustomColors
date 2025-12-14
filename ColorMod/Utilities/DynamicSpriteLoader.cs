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
        private readonly string _dataPath;
        private readonly ConfigurationManager _configManager;

        public DynamicSpriteLoader(string modPath, ConfigurationManager configManager, bool? isDevMode = null)
        {
            _modPath = modPath;
            _configManager = configManager;
            _dataPath = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

            // isDevMode parameter kept for backward compatibility but ignored
            if (isDevMode.HasValue)
            {
                ModLogger.Log("DynamicSpriteLoader: Dev mode parameter is deprecated and ignored");
            }
        }

        /// <summary>
        /// Gets whether the loader is running in dev mode.
        /// </summary>
        [Obsolete("Dev mode is no longer used")]
        public bool IsDevMode()
        {
            return false; // Always return false since dev mode is removed
        }

        /// <summary>
        /// Prepares sprites in the data directory based on current configuration.
        /// All themes are already in the data directory, so just log status and clean up unused ones.
        /// </summary>
        public void PrepareSpritesForConfig()
        {
            ModLogger.Log("[DynamicSpriteLoader] PrepareSpritesForConfig called");

            // All themes already exist in the data directory
            ModLogger.Log("[DynamicSpriteLoader] All themes available in data directory");
            var requiredSchemes = GetRequiredSchemes();
            ModLogger.Log($"DynamicSpriteLoader: Required schemes for current config: {string.Join(", ", requiredSchemes)}");

            // Verify required schemes exist
            VerifyRequiredSchemes(requiredSchemes);

            // Clean up unused schemes (except test themes and story character themes)
            CleanupDataDirectory(requiredSchemes);
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
        /// Verifies that required color schemes exist in the data directory.
        /// </summary>
        private void VerifyRequiredSchemes(HashSet<string> requiredSchemes)
        {
            foreach (var scheme in requiredSchemes)
            {
                var schemeDir = Path.Combine(_dataPath, $"sprites_{scheme}");

                if (Directory.Exists(schemeDir))
                {
                    ModLogger.Log($"DynamicSpriteLoader: Verified sprites_{scheme} exists in data directory");
                }
                else
                {
                    ModLogger.Log($"DynamicSpriteLoader: Warning: sprites_{scheme} not found in data directory");
                }
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
                    ModLogger.Log($"DynamicSpriteLoader: Preserving test theme: {dirName}");
                    continue;
                }

                // Always preserve Orlandeau themes (story character themes)
                if (dirName.StartsWith("sprites_orlandeau_"))
                {
                    ModLogger.Log($"DynamicSpriteLoader: Preserving Orlandeau theme: {dirName}");
                    continue;
                }

                // Always preserve Agrias themes (story character themes)
                if (dirName.StartsWith("sprites_agrias_"))
                {
                    ModLogger.Log($"DynamicSpriteLoader: Preserving Agrias theme: {dirName}");
                    continue;
                }

                // Always preserve Cloud themes (story character themes)
                if (dirName.StartsWith("sprites_cloud_"))
                {
                    ModLogger.Log($"DynamicSpriteLoader: Preserving Cloud theme: {dirName}");
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
                        ModLogger.Log($"DynamicSpriteLoader: Removed unused theme: {dirName}");
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Log($"DynamicSpriteLoader: Failed to remove {dirName}: {ex.Message}");
                    }
                }
            }
        }

    }
}