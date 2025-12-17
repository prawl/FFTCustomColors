using System.ComponentModel;
using System.Linq;
using System.Reflection;
using FFTColorCustomizer.Configuration;
using Xunit;

namespace FFTColorCustomizer.Tests
{
    public class ConfigurationMergeTests
    {
        [Fact]
        public void MergeConfigs_ShouldPreserveExistingValues_WhenIncomingHasDefaults()
        {
            // Arrange
            var existingConfig = new Config
            {
                Squire_Male = "corpse_brigade",
                Knight_Female = "lucavi",
                Archer_Male = "northern_sky",
                WhiteMage_Female = "original"
            };

            // Simulating what Reloaded-II sends: a new config with only one value changed
            var incomingConfig = new Config
            {
                Squire_Male = "original"
                // All other properties remain at their default values (original)
            };

            // Act
            var mergedConfig = ConfigMerger.MergeConfigs(existingConfig, incomingConfig);

            // Assert
            // Since incoming has default (original), existing value should be preserved
            Assert.Equal("corpse_brigade", mergedConfig.Squire_Male);

            // All other values should be preserved from the existing config
            Assert.Equal("lucavi", mergedConfig.Knight_Female);
            Assert.Equal("northern_sky", mergedConfig.Archer_Male);
            Assert.Equal("original", mergedConfig.WhiteMage_Female);
        }
    }
}
