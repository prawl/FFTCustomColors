using System;
using FFTColorMod;
using FFTColorMod.Configuration;
using Xunit;

namespace Tests
{
    public class AgriasThemeCyclingTests
    {
        [Fact]
        public void AgriasColorScheme_HasCorrectNumberOfThemes()
        {
            // Arrange & Act
            var themes = Enum.GetValues<AgriasColorScheme>();

            // Assert - We expect 2 themes: original, ash_dark
            Assert.Equal(2, themes.Length);
        }

        [Fact]
        public void AgriasColorScheme_ContainsExpectedThemes()
        {
            // Arrange
            var themes = Enum.GetValues<AgriasColorScheme>();

            // Act & Assert
            Assert.Contains(AgriasColorScheme.original, themes);
            Assert.Contains(AgriasColorScheme.ash_dark, themes);
        }

        [Fact]
        public void CycleAgriasTheme_CyclesThroughAllThemes()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();
            manager.SetCurrentAgriasTheme(AgriasColorScheme.original);

            // Act & Assert - Cycle through all themes
            var firstCycle = manager.CycleAgriasTheme();
            Assert.Equal(AgriasColorScheme.ash_dark, firstCycle);

            var secondCycle = manager.CycleAgriasTheme();
            Assert.Equal(AgriasColorScheme.original, secondCycle); // Should wrap back to original
        }

        [Fact]
        public void GetCurrentAgriasTheme_ReturnsCurrentTheme()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();

            // Act & Assert - Default should be original
            Assert.Equal(AgriasColorScheme.original, manager.GetCurrentAgriasTheme());

            // Change theme and verify
            manager.SetCurrentAgriasTheme(AgriasColorScheme.ash_dark);
            Assert.Equal(AgriasColorScheme.ash_dark, manager.GetCurrentAgriasTheme());
        }

        [Fact]
        public void SetCurrentAgriasTheme_UpdatesTheme()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();

            // Act
            manager.SetCurrentAgriasTheme(AgriasColorScheme.ash_dark);

            // Assert
            Assert.Equal(AgriasColorScheme.ash_dark, manager.GetCurrentAgriasTheme());
        }

        [Fact]
        public void AgriasTheme_IndependentFromOtherCharacters()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();

            // Act - Set different themes for each character
            manager.SetCurrentAgriasTheme(AgriasColorScheme.ash_dark);
            manager.SetCurrentOrlandeauTheme(OrlandeauColorScheme.thunder_god);

            // Assert - Each character maintains their own theme
            Assert.Equal(AgriasColorScheme.ash_dark, manager.GetCurrentAgriasTheme());
            Assert.Equal(OrlandeauColorScheme.thunder_god, manager.GetCurrentOrlandeauTheme());

            // Act - Cycle one character's theme
            manager.CycleAgriasTheme();

            // Assert - Only Agrias theme changes, others remain the same
            Assert.Equal(AgriasColorScheme.original, manager.GetCurrentAgriasTheme());
            Assert.Equal(OrlandeauColorScheme.thunder_god, manager.GetCurrentOrlandeauTheme());
        }
    }
}