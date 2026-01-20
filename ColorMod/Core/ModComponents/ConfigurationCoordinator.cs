using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Services;
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
        private readonly JobClassDefinitionService? _jobClassService;

        /// <summary>
        /// Creates a ConfigurationCoordinator with explicit dependencies (preferred for DI)
        /// </summary>
        public ConfigurationCoordinator(
            string configPath,
            string modPath,
            JobClassDefinitionService jobClassService)
            : this(configPath, modPath)
        {
            _jobClassService = jobClassService ?? throw new ArgumentNullException(nameof(jobClassService));
        }

        /// <summary>
        /// Creates a ConfigurationCoordinator (falls back to singleton for backward compatibility)
        /// </summary>
        public ConfigurationCoordinator(string configPath, string modPath = null)
        {
            _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
            _configManager = new ConfigurationManager(_configPath);

            // Use provided mod path if available, otherwise try to detect it
            var actualModPath = modPath ?? FFTIVCPathResolver.FindModPathFromConfigPath(_configPath) ?? Path.GetDirectoryName(_configPath);
            ModLogger.Log($"ConfigurationCoordinator using mod path: {actualModPath}");
            // In deployment, source path is the same as mod path
            _spriteManager = new ConfigBasedSpriteManager(actualModPath, _configManager, actualModPath);
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

            // Get all job themes using dictionary-based access
            foreach (var jobKey in config.GetAllJobKeys())
            {
                var value = config.GetJobTheme(jobKey);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    // Convert to display name format
                    result[jobKey] = ConvertThemeNameToDisplayName(value);
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

        /// <summary>
        /// Gets the job color for a specific sprite name.
        /// Uses JobClassService to map sprite names to configuration properties.
        /// </summary>
        /// <param name="spriteName">The sprite filename (e.g., "battle_knight_m_spr.bin")</param>
        /// <returns>The configured color scheme for the job, or null if not found</returns>
        public string? GetJobColorForSprite(string spriteName)
        {
            // Use injected service if available, otherwise fall back to singleton
            var jobClassService = _jobClassService ?? JobClassServiceSingleton.Instance;
            if (jobClassService == null)
            {
                ModLogger.LogDebug($"JobClassService not initialized for sprite: {spriteName}");
                return null;
            }

            var jobClass = jobClassService.GetJobClassBySpriteName(spriteName);
            if (jobClass != null)
            {
                // jobClass.Name is the property name (e.g., "Knight_Male")
                return GetJobColor(jobClass.Name);
            }

            return null;
        }
    }
}
