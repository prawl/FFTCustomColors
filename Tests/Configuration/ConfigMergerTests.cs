using Xunit;
using FFTColorCustomizer.Configuration;

namespace Tests.Configuration
{
    public class ConfigMergerTests
    {
        [Fact]
        public void MergeConfigs_ShouldPreserveExistingRamzaColors()
        {
            // Arrange
            var existingConfig = new Config();
            existingConfig.RamzaColors.Chapter1.HueShift = 45;
            existingConfig.RamzaColors.Chapter1.Enabled = true;
            existingConfig.RamzaColors.Chapter4.LightnessShift = -20;

            var incomingConfig = new Config(); // default RamzaColors

            // Act
            var merged = ConfigMerger.MergeConfigs(existingConfig, incomingConfig);

            // Assert
            Assert.Equal(45, merged.RamzaColors.Chapter1.HueShift);
            Assert.True(merged.RamzaColors.Chapter1.Enabled);
            Assert.Equal(-20, merged.RamzaColors.Chapter4.LightnessShift);
        }

        [Fact]
        public void MergeConfigs_ShouldUseIncomingRamzaColorsWhenNonDefault()
        {
            // Arrange
            var existingConfig = new Config(); // default RamzaColors

            var incomingConfig = new Config();
            incomingConfig.RamzaColors.Chapter2.SaturationShift = 30;
            incomingConfig.RamzaColors.Chapter2.Enabled = true;

            // Act
            var merged = ConfigMerger.MergeConfigs(existingConfig, incomingConfig);

            // Assert
            Assert.Equal(30, merged.RamzaColors.Chapter2.SaturationShift);
            Assert.True(merged.RamzaColors.Chapter2.Enabled);
        }

        [Fact]
        public void MergeConfigs_ShouldMergeEnabledFlagWithOr()
        {
            // Arrange
            var existingConfig = new Config();
            existingConfig.RamzaColors.Chapter1.Enabled = true;

            var incomingConfig = new Config();
            incomingConfig.RamzaColors.Chapter2.Enabled = true;

            // Act
            var merged = ConfigMerger.MergeConfigs(existingConfig, incomingConfig);

            // Assert - both enabled flags should be preserved
            Assert.True(merged.RamzaColors.Chapter1.Enabled);
            Assert.True(merged.RamzaColors.Chapter2.Enabled);
        }
    }
}
