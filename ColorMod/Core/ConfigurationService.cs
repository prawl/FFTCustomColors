using System;
using System.IO;
using System.Text.Json;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Interfaces;
using static FFTColorCustomizer.Core.ColorModConstants;

namespace FFTColorCustomizer.Core
{
    /// <summary>
    /// Service for managing mod configuration
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        private readonly IPathResolver _pathResolver;
        private readonly JsonSerializerOptions _serializerOptions;

        public ConfigurationService(IPathResolver pathResolver)
        {
            _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
            // Use the same serializer options as Configurable to ensure custom converters are used
            _serializerOptions = Configurable<Config>.SerializerOptions;
        }

        /// <inheritdoc />
        public Config LoadConfig()
        {
            var configPath = _pathResolver.GetConfigPath();

            if (!File.Exists(configPath))
            {
                var defaultConfig = GetDefaultConfig();
                SaveConfig(defaultConfig);
                return defaultConfig;
            }

            try
            {
                var json = File.ReadAllText(configPath);
                return JsonSerializer.Deserialize<Config>(json, _serializerOptions) ?? GetDefaultConfig();
            }
            catch
            {
                return GetDefaultConfig();
            }
        }

        /// <inheritdoc />
        public void SaveConfig(Config config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var configPath = _pathResolver.GetConfigPath();
            var directory = Path.GetDirectoryName(configPath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(config, _serializerOptions);
            File.WriteAllText(configPath, json);
        }

        /// <inheritdoc />
        public Config GetDefaultConfig()
        {
            // Config constructor already initializes all job and story character themes to "original"
            return new Config();
        }

        /// <inheritdoc />
        public void ResetToDefaults()
        {
            var defaultConfig = GetDefaultConfig();
            SaveConfig(defaultConfig);
        }

        /// <inheritdoc />
        public bool ValidateConfig(Config config)
        {
            if (config == null)
                return false;

            // Validate all job themes have non-null values
            foreach (var jobKey in config.GetAllJobKeys())
            {
                var value = config.GetJobTheme(jobKey);
                if (string.IsNullOrWhiteSpace(value))
                    return false;
            }

            // Validate all story character themes have non-null values
            foreach (var character in config.GetAllStoryCharacters())
            {
                var value = config.GetStoryCharacterTheme(character);
                if (string.IsNullOrWhiteSpace(value))
                    return false;
            }

            return true;
        }
    }
}
