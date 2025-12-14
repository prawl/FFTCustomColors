using System.ComponentModel;
using System.Linq;
using System.Reflection;
using FFTColorMod.Configuration;
using Xunit;

namespace FFTColorMod.Tests
{
    public class ConfigurationMergeTests
    {
        [Fact]
        public void MergeConfigs_ShouldPreserveExistingValues_WhenIncomingHasDefaults()
        {
            // Arrange
            var existingConfig = new Config
            {
                Squire_Male = FFTColorMod.Configuration.ColorScheme.corpse_brigade,
                Knight_Female = FFTColorMod.Configuration.ColorScheme.lucavi,
                Archer_Male = FFTColorMod.Configuration.ColorScheme.northern_sky,
                WhiteMage_Female = FFTColorMod.Configuration.ColorScheme.crimson_red
            };

            // Simulating what Reloaded-II sends: a new config with only one value changed
            var incomingConfig = new Config
            {
                Squire_Male = FFTColorMod.Configuration.ColorScheme.phoenix_flame
                // All other properties remain at their default values (original)
            };

            // Act
            var mergedConfig = ConfigMerger.MergeConfigs(existingConfig, incomingConfig);

            // Assert
            // The changed value should be updated
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.phoenix_flame, mergedConfig.Squire_Male);

            // All other values should be preserved from the existing config
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.lucavi, mergedConfig.Knight_Female);
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.northern_sky, mergedConfig.Archer_Male);
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.crimson_red, mergedConfig.WhiteMage_Female);
        }
    }
}