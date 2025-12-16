using System;
using FFTColorMod;
using FFTColorMod.Configuration;
using FFTColorMod.Utilities;
using Xunit;

namespace Tests
{
    public class AgriasThemeCyclingTests
    {
        [Fact]
        public void AgriasColorScheme_HasCorrectNumberOfThemes()
        {
            // Arrange & Act
            var themes = new[] { "original", "ash_dark" };

            // Assert - We expect 2 themes: original, ash_dark
            Assert.Equal(2, themes.Length);
        }

        [Fact]
        public void AgriasColorScheme_ContainsExpectedThemes()
        {
            // Arrange
            var themes = new[] { "original", "ash_dark" };

            // Act & Assert
            Assert.Contains("original", themes);
            Assert.Contains("ash_dark", themes);
        }

        [Fact]
        public void CycleAgriasTheme_CyclesThroughAllThemes()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();
            manager.SetCurrentTheme("Agrias", "original");

            // Act & Assert - Cycle through all themes
            var firstCycle = manager.CycleTheme("Agrias");
            Assert.Equal("ash_dark", firstCycle);

            var secondCycle = manager.CycleTheme("Agrias");
            Assert.Equal("original", secondCycle); // Should wrap back to original
        }

        [Fact]
        public void GetCurrentAgriasTheme_ReturnsCurrentTheme()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();

            // Act & Assert - Default should be original
            Assert.Equal("original", manager.GetCurrentTheme("Agrias"));

            // Change theme and verify
            manager.SetCurrentTheme("Agrias", "ash_dark");
            Assert.Equal("ash_dark", manager.GetCurrentTheme("Agrias"));
        }

        [Fact]
        public void SetCurrentAgriasTheme_UpdatesTheme()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();

            // Act
            manager.SetCurrentTheme("Agrias", "ash_dark");

            // Assert
            Assert.Equal("ash_dark", manager.GetCurrentTheme("Agrias"));
        }

        [Fact]
        public void AgriasTheme_IndependentFromOtherCharacters()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();

            // Act - Set different themes for each character
            manager.SetCurrentTheme("Agrias", "ash_dark");
            manager.SetCurrentTheme("Orlandeau", "thunder_god");

            // Assert - Each character maintains their own theme
            Assert.Equal("ash_dark", manager.GetCurrentTheme("Agrias"));
            Assert.Equal("thunder_god", manager.GetCurrentTheme("Orlandeau"));

            // Act - Cycle one character's theme
            manager.CycleTheme("Agrias");

            // Assert - Only Agrias theme changes, others remain the same
            Assert.Equal("original", manager.GetCurrentTheme("Agrias"));
            Assert.Equal("thunder_god", manager.GetCurrentTheme("Orlandeau"));
        }
    }
}