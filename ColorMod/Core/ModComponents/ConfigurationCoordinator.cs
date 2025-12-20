using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Utilities;
using static FFTColorCustomizer.Core.ColorModConstants;

namespace FFTColorCustomizer.Core.ModComponents
{
    /// <summary>
    /// Coordinates all configuration-related operations
    /// </summary>
    public class ConfigurationCoordinator
    {
        private readonly string _configPath;
        private readonly ConfigurationManager _configManager;
        private readonly ConfigBasedSpriteManager _spriteManager;

        public ConfigurationCoordinator(string configPath, string modPath = null)
        {
            _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
            _configManager = new ConfigurationManager(_configPath);

            // Use provided mod path if available, otherwise try to detect it
            var actualModPath = modPath ?? GetActualModPath(_configPath);
            ModLogger.Log($"ConfigurationCoordinator using mod path: {actualModPath}");
            // In deployment, source path is the same as mod path
            _spriteManager = new ConfigBasedSpriteManager(actualModPath, _configManager, actualModPath);
        }

        private string GetActualModPath(string configPath)
        {
            // If config path is in User directory, find the actual mod installation
            if (configPath.Contains(@"User\Mods") || configPath.Contains(@"User/Mods"))
            {
                var configDir = Path.GetDirectoryName(configPath);
                if (configDir != null)
                {
                    var userModsIdx = configDir.IndexOf(@"User\Mods", StringComparison.OrdinalIgnoreCase);
                    if (userModsIdx == -1)
                        userModsIdx = configDir.IndexOf(@"User/Mods", StringComparison.OrdinalIgnoreCase);

                    if (userModsIdx >= 0)
                    {
                        var reloadedRoot = configDir.Substring(0, userModsIdx);
                        var modsDir = Path.Combine(reloadedRoot, "Mods");

                        // First try the non-versioned path
                        var directPath = Path.Combine(modsDir, "FFTColorCustomizer");
                        if (Directory.Exists(directPath))
                        {
                            return directPath;
                        }

                        // Look for versioned directories and use the highest version
                        try
                        {
                            var versionedDirs = Directory.GetDirectories(modsDir, "FFTColorCustomizer_v*")
                                .OrderByDescending(dir =>
                                {
                                    var dirName = Path.GetFileName(dir);
                                    var versionStr = dirName.Substring(dirName.LastIndexOf('v') + 1);
                                    if (int.TryParse(versionStr, out int version))
                                        return version;
                                    return 0;
                                })
                                .ToArray();

                            if (versionedDirs.Length > 0)
                            {
                                ModLogger.Log($"Found versioned mod directory: {versionedDirs[0]}");
                                return versionedDirs[0];
                            }
                        }
                        catch (Exception ex)
                        {
                            ModLogger.LogWarning($"Error searching for versioned directories: {ex.Message}");
                        }

                        // Fallback to non-versioned path
                        return directPath;
                    }
                }
            }

            // Fallback to config directory
            return Path.GetDirectoryName(configPath);
        }

        /// <summary>
        /// Get the current configuration
        /// </summary>
        public Config GetConfiguration()
        {
            return _configManager.LoadConfig();
        }

        /// <summary>
        /// Set a job's color scheme
        /// </summary>
        public void SetJobColor(string jobProperty, string colorScheme)
        {
            _configManager.SetColorSchemeForJob(jobProperty, colorScheme);
            _spriteManager.SetColorForJob(jobProperty, colorScheme);
        }

        /// <summary>
        /// Get a job's current color scheme
        /// </summary>
        public string GetJobColor(string jobProperty)
        {
            return _spriteManager.GetActiveColorForJob(jobProperty);
        }

        /// <summary>
        /// Get all job colors
        /// </summary>
        public Dictionary<string, string> GetAllJobColors()
        {
            var result = new Dictionary<string, string>();
            var config = GetConfiguration();

            // Get all properties from Config class
            var properties = typeof(Config).GetProperties()
                .Where(p => p.PropertyType == typeof(string));

            foreach (var property in properties)
            {
                var value = property.GetValue(config) as string;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    // Convert to display name format
                    result[property.Name] = ConvertThemeNameToDisplayName(value);
                }
            }

            return result;
        }

        /// <summary>
        /// Reset all colors to defaults
        /// </summary>
        public void ResetToDefaults()
        {
            _configManager.ResetToDefaults();
            _spriteManager.ResetAllToOriginal();
        }

        /// <summary>
        /// Save the current configuration
        /// </summary>
        public void SaveConfiguration()
        {
            var config = GetConfiguration();
            _configManager.SaveConfig(config);
        }

        /// <summary>
        /// Load configuration from file
        /// </summary>
        public void LoadConfiguration()
        {
            var config = _configManager.LoadConfig();
            UpdateConfiguration(config);
        }

        /// <summary>
        /// Update the configuration
        /// </summary>
        public void UpdateConfiguration(Config newConfig)
        {
            _configManager.SaveConfig(newConfig);
            _spriteManager.UpdateConfiguration(newConfig);
        }

        /// <summary>
        /// Check if configuration manager is available
        /// </summary>
        public bool HasConfigurationManager()
        {
            return _configManager != null;
        }

        /// <summary>
        /// Get available themes
        /// </summary>
        public IList<string> GetAvailableThemes()
        {
            return _configManager.GetAvailableColorSchemes();
        }

        /// <summary>
        /// Apply configuration to sprites
        /// </summary>
        public void ApplyConfiguration()
        {
            _spriteManager.ApplyConfiguration();
        }

        /// <summary>
        /// Get the sprite manager
        /// </summary>
        public ConfigBasedSpriteManager GetSpriteManager()
        {
            return _spriteManager;
        }

        /// <summary>
        /// Get the configuration manager
        /// </summary>
        public ConfigurationManager GetConfigurationManager()
        {
            return _configManager;
        }

        /// <summary>
        /// Open configuration UI
        /// </summary>
        public void OpenConfigurationUI(Action<Config> onConfigUpdated)
        {
            try
            {
                var config = GetConfiguration();
                if (config != null)
                {
                    // Use the already determined actual mod path instead of trying to resolve it again
                    string actualModPath = _spriteManager.GetModPath();

                    ModLogger.LogDebug($"OpenConfigurationUI - configPath: {_configPath}");
                    ModLogger.LogDebug($"OpenConfigurationUI - actualModPath: {actualModPath}");
                    ModLogger.LogDebug($"Resources/Previews path: {Path.Combine(actualModPath, "Resources", "Previews")}");
                    ModLogger.LogDebug($"Directory exists: {Directory.Exists(Path.Combine(actualModPath, "Resources", "Previews"))}");

                    var form = new ConfigurationForm(config, _configPath, actualModPath);

                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        onConfigUpdated?.Invoke(form.Configuration);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"Failed to open configuration UI: {ex.Message}");
            }
        }

        /// <summary>
        /// Convert theme name to display format
        /// </summary>
        private string ConvertThemeNameToDisplayName(string themeName)
        {
            if (string.IsNullOrEmpty(themeName))
                return "Original";

            // Replace underscores with spaces and convert to title case
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
                themeName.Replace('_', ' ')
            );
        }
    }
}
