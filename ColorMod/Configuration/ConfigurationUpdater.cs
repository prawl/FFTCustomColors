using System.IO;
using System.Text.Json;

namespace FFTColorMod.Configuration
{
    public class ConfigurationUpdater
    {
        public void UpdateAndSaveConfiguration(Config incomingConfig, string configPath)
        {
            // Load existing configuration
            var existingJson = File.ReadAllText(configPath);
            var existingConfig = JsonSerializer.Deserialize<Config>(existingJson,
                Configurable<Config>.SerializerOptions);

            // Merge and save
            var mergedConfig = ConfigMerger.MergeConfigs(existingConfig, incomingConfig);
            var json = JsonSerializer.Serialize(mergedConfig, Configurable<Config>.SerializerOptions);
            File.WriteAllText(configPath, json);
        }
    }
}