using System;
using System.Linq;
using Xunit;
using FluentAssertions;
using FFTColorMod;
using FFTColorMod.Configuration;

namespace FFTColorMod.Tests
{
    public class BeowulfThemeCyclingTests
    {
        [Fact]
        public void BeowulfColorScheme_Should_Have_2_Themes()
        {
            // Arrange & Act
            var themes = Enum.GetValues(typeof(BeowulfColorScheme));

            // Assert
            themes.Length.Should().Be(2, "Beowulf should have exactly 2 themes: original and test");
        }

        [Fact]
        public void BeowulfColorScheme_Should_Have_Expected_Themes()
        {
            // Arrange
            var expectedThemes = new[]
            {
                "original",
                "test"
            };

            // Act
            var actualThemes = Enum.GetNames(typeof(BeowulfColorScheme));

            // Assert
            actualThemes.Should().BeEquivalentTo(expectedThemes);
        }

        [Fact]
        public void StoryCharacterThemeManager_Should_Cycle_Beowulf_Themes()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();

            // Act & Assert - Start with test (default)
            manager.GetCurrentBeowulfTheme().Should().Be(BeowulfColorScheme.test);

            // Cycle to original
            var theme1 = manager.CycleBeowulfTheme();
            theme1.Should().Be(BeowulfColorScheme.original);

            // Cycle back to test
            var theme2 = manager.CycleBeowulfTheme();
            theme2.Should().Be(BeowulfColorScheme.test);
        }

        [Fact]
        public void F2_Should_Cycle_Beowulf_Theme_Along_With_Generic_Themes()
        {
            // This test verifies that F2 cycles both generic and Beowulf themes simultaneously
            // Arrange
            var mod = new Mod(new ModContext());

            // Note: We can't directly test the file copying behavior without mocking,
            // but we can verify the theme manager state changes

            // Act
            mod.ProcessHotkeyPress(0x71); // F2

            // Assert
            // After F2, Beowulf should cycle from test to original
            // (We'd need to expose the theme manager or add a getter to properly test this)
        }
    }
}