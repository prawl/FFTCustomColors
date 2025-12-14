using System.IO;
using System.Text.Json;
using FFTColorMod.Configuration;
using Xunit;

namespace FFTColorMod.Tests
{
    public class OnConfigurationUpdatedTests
    {
        [Fact]
        public void ConfigurationUpdater_ShouldPreserveExistingSettings_WhenOneValueChanges()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var configPath = Path.Combine(tempDir, "Config.json");
            Directory.CreateDirectory(tempDir);

            // Create existing config with multiple settings
            var existingConfig = new Config
            {
                Squire_Male = FFTColorMod.Configuration.ColorScheme.corpse_brigade,
                Knight_Female = FFTColorMod.Configuration.ColorScheme.lucavi,
                Archer_Male = FFTColorMod.Configuration.ColorScheme.northern_sky
            };
            var existingJson = JsonSerializer.Serialize(existingConfig, Configurable<Config>.SerializerOptions);
            File.WriteAllText(configPath, existingJson);

            try
            {
                // Simulate Reloaded-II sending an update with only one changed value
                var incomingConfig = new Config
                {
                    Squire_Male = FFTColorMod.Configuration.ColorScheme.phoenix_flame
                    // All other values are defaults
                };

                // Act - Use ConfigurationUpdater to handle the merge and save
                var updater = new ConfigurationUpdater();
                updater.UpdateAndSaveConfiguration(incomingConfig, configPath);

                // Assert - Read the saved config and verify all values are preserved
                var savedJson = File.ReadAllText(configPath);
                var savedConfig = JsonSerializer.Deserialize<Config>(savedJson, Configurable<Config>.SerializerOptions);

                Assert.Equal(FFTColorMod.Configuration.ColorScheme.phoenix_flame, savedConfig.Squire_Male); // Changed value
                Assert.Equal(FFTColorMod.Configuration.ColorScheme.lucavi, savedConfig.Knight_Female); // Preserved
                Assert.Equal(FFTColorMod.Configuration.ColorScheme.northern_sky, savedConfig.Archer_Male); // Preserved
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}