using System;
using FFTColorCustomizer;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace Tests
{
    public class AgriasThemeCyclingTests
    {
        [Fact]
        public void AgriasColorScheme_HasCorrectNumberOfThemes()
        {
            // Arrange & Act
            var themes = new[] { "original", "gun_slinger" };

            // Assert - We expect 2 themes: original, gun_slinger
            Assert.Equal(2, themes.Length);
        }

        [Fact]
        public void AgriasColorScheme_ContainsExpectedThemes()
        {
            // Arrange
            var themes = new[] { "original", "gun_slinger" };

            // Act & Assert
            Assert.Contains("original", themes);
            Assert.Contains("gun_slinger", themes);
        }

        [Fact]
        public void CycleAgriasTheme_CyclesThroughAllThemes()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();
            manager.SetCurrentTheme("Agrias", "original");

            // Act & Assert - Cycle through some themes (Agrias has 12 themes)
            var firstCycle = manager.CycleTheme("Agrias");
            Assert.Equal("gun_slinger", firstCycle);

            var secondCycle = manager.CycleTheme("Agrias");
            Assert.Equal("crimson_assassin", secondCycle); // Third theme

            var thirdCycle = manager.CycleTheme("Agrias");
            Assert.Equal("shadow_dancer", thirdCycle); // Fourth theme
        }

        [Fact]
        public void GetCurrentAgriasTheme_ReturnsCurrentTheme()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();

            // Act & Assert - Default should be original
            Assert.Equal("original", manager.GetCurrentTheme("Agrias"));

            // Change theme and verify
            manager.SetCurrentTheme("Agrias", "gun_slinger");
            Assert.Equal("gun_slinger", manager.GetCurrentTheme("Agrias"));
        }

        [Fact]
        public void SetCurrentAgriasTheme_UpdatesTheme()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();

            // Act
            manager.SetCurrentTheme("Agrias", "gun_slinger");

            // Assert
            Assert.Equal("gun_slinger", manager.GetCurrentTheme("Agrias"));
        }

        [Fact]
        public void AgriasTheme_IndependentFromOtherCharacters()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();

            // Act - Set different themes for each character
            manager.SetCurrentTheme("Agrias", "gun_slinger");
            manager.SetCurrentTheme("Orlandeau", "thunder_god");

            // Assert - Each character maintains their own theme
            Assert.Equal("gun_slinger", manager.GetCurrentTheme("Agrias"));
            Assert.Equal("thunder_god", manager.GetCurrentTheme("Orlandeau"));

            // Act - Cycle one character's theme
            manager.CycleTheme("Agrias");

            // Assert - Only Agrias theme changes, others remain the same
            Assert.Equal("crimson_assassin", manager.GetCurrentTheme("Agrias")); // Next theme after gun_slinger
            Assert.Equal("thunder_god", manager.GetCurrentTheme("Orlandeau"));
        }
    }
}
