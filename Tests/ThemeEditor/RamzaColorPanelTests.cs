using Xunit;
using FFTColorCustomizer.ThemeEditor;
using FFTColorCustomizer.Configuration;

namespace Tests.ThemeEditor
{
    public class RamzaColorPanelTests
    {
        [Fact]
        public void Constructor_ShouldCreatePanelWithDefaultValues()
        {
            // Arrange & Act
            var panel = new RamzaColorPanel();

            // Assert
            Assert.Equal(0, panel.HueShift);
            Assert.Equal(0, panel.SaturationShift);
            Assert.Equal(0, panel.LightnessShift);
            Assert.False(panel.Enabled);
        }

        [Fact]
        public void Chapter_ShouldUpdateChapterProperty()
        {
            // Arrange
            var panel = new RamzaColorPanel();

            // Act
            panel.Chapter = 4;

            // Assert
            Assert.Equal(4, panel.Chapter);
        }

        [Fact]
        public void LoadSettings_ShouldPopulateAllValues()
        {
            // Arrange
            var panel = new RamzaColorPanel();
            var settings = new RamzaChapterHslSettings
            {
                Enabled = true,
                HueShift = 90,
                SaturationShift = -25,
                LightnessShift = 15
            };

            // Act
            panel.LoadSettings(settings);

            // Assert
            Assert.True(panel.Enabled);
            Assert.Equal(90, panel.HueShift);
            Assert.Equal(-25, panel.SaturationShift);
            Assert.Equal(15, panel.LightnessShift);
        }

        [Fact]
        public void GetSettings_ShouldReturnCurrentValues()
        {
            // Arrange
            var panel = new RamzaColorPanel();
            panel.Enabled = true;
            panel.HueShift = -45;
            panel.SaturationShift = 30;
            panel.LightnessShift = -10;

            // Act
            var settings = panel.GetSettings();

            // Assert
            Assert.True(settings.Enabled);
            Assert.Equal(-45, settings.HueShift);
            Assert.Equal(30, settings.SaturationShift);
            Assert.Equal(-10, settings.LightnessShift);
        }

        [Fact]
        public void LoadSettings_WithNullSettings_ShouldNotThrow()
        {
            // Arrange
            var panel = new RamzaColorPanel();

            // Act & Assert - should not throw
            panel.LoadSettings(null);
            Assert.Equal(0, panel.HueShift);
        }

        [Fact]
        public void HueShift_ShouldClampToValidRange()
        {
            // Arrange
            var panel = new RamzaColorPanel();

            // Act - set value beyond max
            panel.HueShift = 200;

            // Assert - should be clamped to 180
            Assert.Equal(180, panel.HueShift);

            // Act - set value beyond min
            panel.HueShift = -200;

            // Assert - should be clamped to -180
            Assert.Equal(-180, panel.HueShift);
        }

        [Fact]
        public void SaturationShift_ShouldClampToValidRange()
        {
            // Arrange
            var panel = new RamzaColorPanel();

            // Act - set value beyond max
            panel.SaturationShift = 150;

            // Assert - should be clamped to 100
            Assert.Equal(100, panel.SaturationShift);

            // Act - set value beyond min
            panel.SaturationShift = -150;

            // Assert - should be clamped to -100
            Assert.Equal(-100, panel.SaturationShift);
        }

        [Fact]
        public void LightnessShift_ShouldClampToValidRange()
        {
            // Arrange
            var panel = new RamzaColorPanel();

            // Act - set value beyond max
            panel.LightnessShift = 150;

            // Assert - should be clamped to 100
            Assert.Equal(100, panel.LightnessShift);

            // Act - set value beyond min
            panel.LightnessShift = -150;

            // Assert - should be clamped to -100
            Assert.Equal(-100, panel.LightnessShift);
        }

        [Fact]
        public void LoadSettings_ShouldNotFireSettingsChangedEvent()
        {
            // Arrange
            var panel = new RamzaColorPanel();
            var eventFired = false;
            panel.SettingsChanged += (s, e) => eventFired = true;
            var settings = new RamzaChapterHslSettings
            {
                Enabled = true,
                HueShift = 90,
                SaturationShift = -25,
                LightnessShift = 15
            };

            // Act
            panel.LoadSettings(settings);

            // Assert - event should not fire during load
            Assert.False(eventFired);
        }
    }
}
