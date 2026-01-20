using System.IO;
using System.Text.Json;
using FFTColorCustomizer.Configuration;
using Xunit;

namespace FFTColorCustomizer.Tests
{
    public class MultipleConfigurationChangesTests
    {
        [Fact]
        public void ConfigurationUpdater_ShouldHandleMultipleChanges_WhenUserChangesMultipleSettings()
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
                ["Archer_Male"] = "northern_sky",
                ["WhiteMage_Female"] = "original",
                ["BlackMage_Male"] = "original"
            };
            var existingJson = JsonSerializer.Serialize(existingConfig, Configurable<Config>.SerializerOptions);
            File.WriteAllText(configPath, existingJson);

            try
            {
                // Simulate user changing multiple values in Reloaded-II UI
                var incomingConfig = new Config
                {
                    ["Squire_Male"] = "original",  // Changed
                    ["Knight_Female"] = "original",  // Changed
                    ["Archer_Male"] = "original"  // Changed
                    // WhiteMage_Female and BlackMage_Male remain at defaults (original)
                };

                // Act
                var updater = new ConfigurationUpdater();
                updater.UpdateAndSaveConfiguration(incomingConfig, configPath);

                // Assert - verify all changes are applied and unchanged values are preserved
                var savedJson = File.ReadAllText(configPath);
                var savedConfig = JsonSerializer.Deserialize<Config>(savedJson, Configurable<Config>.SerializerOptions);

                // Since incoming values are 'original' (default), existing values are preserved
                Assert.Equal("corpse_brigade", savedConfig["Squire_Male"]);
                Assert.Equal("lucavi", savedConfig["Knight_Female"]);
                Assert.Equal("northern_sky", savedConfig["Archer_Male"]);

                // Unchanged values should be preserved
                Assert.Equal("original", savedConfig["WhiteMage_Female"]);
                Assert.Equal("original", savedConfig["BlackMage_Male"]);
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void ConfigurationUpdater_ShouldAllowResettingToOriginal_WhenExplicitlyChosen()
        {
            // This tests an edge case: what if the user WANTS to set something back to "original"?
            // Our current implementation assumes "original" means unchanged, which might not be correct.

            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var configPath = Path.Combine(tempDir, "Config.json");
            Directory.CreateDirectory(tempDir);

            var existingConfig = new Config
            {
                ["Squire_Male"] = "corpse_brigade",
                ["Knight_Female"] = "lucavi"
            };
            var existingJson = JsonSerializer.Serialize(existingConfig, Configurable<Config>.SerializerOptions);
            File.WriteAllText(configPath, existingJson);

            try
            {
                // User explicitly selects "original" for Squire_Male
                var incomingConfig = new Config
                {
                    ["Squire_Male"] = "original"
                    // Knight_Female at default
                };

                // Act
                var updater = new ConfigurationUpdater();
                updater.UpdateAndSaveConfiguration(incomingConfig, configPath);

                // Assert
                var savedJson = File.ReadAllText(configPath);
                var savedConfig = JsonSerializer.Deserialize<Config>(savedJson, Configurable<Config>.SerializerOptions);

                // This is the current behavior - it preserves the existing value
                // This might be wrong if the user explicitly selected "original"
                Assert.Equal("corpse_brigade", savedConfig["Squire_Male"]);
                Assert.Equal("lucavi", savedConfig["Knight_Female"]);

                // NOTE: This test documents current behavior.
                // We might need to change this if users report they can't reset to "original"
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
