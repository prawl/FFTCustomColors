using Xunit;
using FFTColorCustomizer.Configuration;

namespace Tests.Configuration
{
    public class RamzaHslSettingsTests
    {
        [Fact]
        public void RamzaChapterHslSettings_DefaultConstructor_ShouldHaveZeroShifts()
        {
            // Arrange & Act
            var settings = new RamzaChapterHslSettings();

            // Assert
            Assert.Equal(0, settings.HueShift);
            Assert.Equal(0, settings.SaturationShift);
            Assert.Equal(0, settings.LightnessShift);
            Assert.False(settings.Enabled);
        }

        [Fact]
        public void RamzaHslSettings_DefaultConstructor_ShouldHaveAllChapters()
        {
            // Arrange & Act
            var settings = new RamzaHslSettings();

            // Assert
            Assert.NotNull(settings.Chapter1);
            Assert.NotNull(settings.Chapter2);
            Assert.NotNull(settings.Chapter4);
        }

        [Fact]
        public void RamzaHslSettings_Reset_ShouldRestoreDefaults()
        {
            // Arrange
            var settings = new RamzaHslSettings();
            settings.Chapter1.HueShift = 90;
            settings.Chapter1.SaturationShift = 50;
            settings.Chapter1.Enabled = true;
            settings.Chapter2.HueShift = -45;

            // Act
            settings.Reset();

            // Assert
            Assert.Equal(0, settings.Chapter1.HueShift);
            Assert.Equal(0, settings.Chapter1.SaturationShift);
            Assert.False(settings.Chapter1.Enabled);
            Assert.Equal(0, settings.Chapter2.HueShift);
        }

        [Fact]
        public void RamzaHslSettings_ChaptersAreIndependent()
        {
            // Arrange
            var settings = new RamzaHslSettings();

            // Act - modify only Chapter1
            settings.Chapter1.HueShift = 180;
            settings.Chapter1.Enabled = true;

            // Assert - other chapters unchanged
            Assert.Equal(180, settings.Chapter1.HueShift);
            Assert.True(settings.Chapter1.Enabled);
            Assert.Equal(0, settings.Chapter2.HueShift);
            Assert.False(settings.Chapter2.Enabled);
            Assert.Equal(0, settings.Chapter4.HueShift);
            Assert.False(settings.Chapter4.Enabled);
        }
    }
}
