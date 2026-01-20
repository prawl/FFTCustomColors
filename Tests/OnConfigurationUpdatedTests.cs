using System.IO;
using System.Text.Json;
using FFTColorCustomizer.Configuration;
using Xunit;

namespace FFTColorCustomizer.Tests
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
                ["Squire_Male"] = "corpse_brigade",
                ["Knight_Female"] = "lucavi",
                ["Archer_Male"] = "northern_sky"
            };
            var existingJson = JsonSerializer.Serialize(existingConfig, Configurable<Config>.SerializerOptions);
            File.WriteAllText(configPath, existingJson);

            try
            {
                // Simulate Reloaded-II sending an update with only one changed value
                var incomingConfig = new Config
                {
                    ["Squire_Male"] = "original"
                    // All other values are defaults
                };

                // Act - Use ConfigurationUpdater to handle the merge and save
                var updater = new ConfigurationUpdater();
                updater.UpdateAndSaveConfiguration(incomingConfig, configPath);

                // Assert - Read the saved config and verify all values are preserved
                var savedJson = File.ReadAllText(configPath);
                var savedConfig = JsonSerializer.Deserialize<Config>(savedJson, Configurable<Config>.SerializerOptions);

                Assert.Equal("corpse_brigade", savedConfig["Squire_Male"]); // Preserved since incoming is default
                Assert.Equal("lucavi", savedConfig["Knight_Female"]); // Preserved
                Assert.Equal("northern_sky", savedConfig["Archer_Male"]); // Preserved
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
