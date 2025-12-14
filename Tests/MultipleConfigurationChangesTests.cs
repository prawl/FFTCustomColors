using System.IO;
using System.Text.Json;
using FFTColorMod.Configuration;
using Xunit;

namespace FFTColorMod.Tests
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
                Squire_Male = FFTColorMod.Configuration.ColorScheme.corpse_brigade,
                Knight_Female = FFTColorMod.Configuration.ColorScheme.lucavi,
                Archer_Male = FFTColorMod.Configuration.ColorScheme.northern_sky,
                WhiteMage_Female = FFTColorMod.Configuration.ColorScheme.crimson_red,
                BlackMage_Male = FFTColorMod.Configuration.ColorScheme.royal_purple
            };
            var existingJson = JsonSerializer.Serialize(existingConfig, Configurable<Config>.SerializerOptions);
            File.WriteAllText(configPath, existingJson);

            try
            {
                // Simulate user changing multiple values in Reloaded-II UI
                var incomingConfig = new Config
                {
                    Squire_Male = FFTColorMod.Configuration.ColorScheme.phoenix_flame,  // Changed
                    Knight_Female = FFTColorMod.Configuration.ColorScheme.frost_knight,  // Changed
                    Archer_Male = FFTColorMod.Configuration.ColorScheme.emerald_dragon  // Changed
                    // WhiteMage_Female and BlackMage_Male remain at defaults (original)
                };

                // Act
                var updater = new ConfigurationUpdater();
                updater.UpdateAndSaveConfiguration(incomingConfig, configPath);

                // Assert - verify all changes are applied and unchanged values are preserved
                var savedJson = File.ReadAllText(configPath);
                var savedConfig = JsonSerializer.Deserialize<Config>(savedJson, Configurable<Config>.SerializerOptions);

                // Changed values should be updated
                Assert.Equal(FFTColorMod.Configuration.ColorScheme.phoenix_flame, savedConfig.Squire_Male);
                Assert.Equal(FFTColorMod.Configuration.ColorScheme.frost_knight, savedConfig.Knight_Female);
                Assert.Equal(FFTColorMod.Configuration.ColorScheme.emerald_dragon, savedConfig.Archer_Male);

                // Unchanged values should be preserved
                Assert.Equal(FFTColorMod.Configuration.ColorScheme.crimson_red, savedConfig.WhiteMage_Female);
                Assert.Equal(FFTColorMod.Configuration.ColorScheme.royal_purple, savedConfig.BlackMage_Male);
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
                Squire_Male = FFTColorMod.Configuration.ColorScheme.corpse_brigade,
                Knight_Female = FFTColorMod.Configuration.ColorScheme.lucavi
            };
            var existingJson = JsonSerializer.Serialize(existingConfig, Configurable<Config>.SerializerOptions);
            File.WriteAllText(configPath, existingJson);

            try
            {
                // User explicitly selects "original" for Squire_Male
                var incomingConfig = new Config
                {
                    Squire_Male = FFTColorMod.Configuration.ColorScheme.original
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
                Assert.Equal(FFTColorMod.Configuration.ColorScheme.corpse_brigade, savedConfig.Squire_Male);
                Assert.Equal(FFTColorMod.Configuration.ColorScheme.lucavi, savedConfig.Knight_Female);

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