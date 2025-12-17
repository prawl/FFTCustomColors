using FFTColorMod.Configuration;

namespace FFTColorMod.Interfaces
{
    /// <summary>
    /// Service for managing mod configuration
    /// </summary>
    public interface IConfigurationService
    {
        /// <summary>
        /// Loads the configuration from storage
        /// </summary>
        Config LoadConfig();

        /// <summary>
        /// Saves the configuration to storage
        /// </summary>
        void SaveConfig(Config config);

        /// <summary>
        /// Gets the default configuration
        /// </summary>
        Config GetDefaultConfig();

        /// <summary>
        /// Resets the configuration to defaults
        /// </summary>
        void ResetToDefaults();

        /// <summary>
        /// Validates a configuration
        /// </summary>
        bool ValidateConfig(Config config);
    }
}