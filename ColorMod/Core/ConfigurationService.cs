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
            _serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
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
            var config = new Config();

            // Set all job properties to "original"
            var properties = typeof(Config).GetProperties();
            foreach (var property in properties)
            {
                if (property.PropertyType == typeof(string) && property.CanWrite)
                {
                    property.SetValue(config, DefaultTheme);
                }
            }

            return config;
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

            // Basic validation - ensure all string properties have non-null values
            var properties = typeof(Config).GetProperties();
            foreach (var property in properties)
            {
                if (property.PropertyType == typeof(string))
                {
                    var value = property.GetValue(config) as string;
                    if (string.IsNullOrWhiteSpace(value))
                        return false;
                }
            }

            return true;
        }
    }
}
