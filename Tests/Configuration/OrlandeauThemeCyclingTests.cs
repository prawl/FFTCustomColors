using Xunit;
using FluentAssertions;
using FFTColorMod;
using FFTColorMod.Configuration;
using FFTColorMod.Utilities;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;

namespace FFTColorMod.Tests
{
    public class OrlandeauThemeCyclingTests
    {
        [Fact]
        public void OrlandeauThemeManager_Should_Have_At_Least_Original_Theme()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();

            // Act
            var availableThemes = manager.GetAvailableThemes("Orlandeau");

            // Assert
            availableThemes.Should().NotBeEmpty("Orlandeau should have at least one theme");
            availableThemes.Should().Contain("original", "Orlandeau themes should always include 'original'");
        }

        [Fact]
        public void OrlandeauThemes_Should_Include_Original_But_Not_Generic_Themes()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();
            var genericThemes = new[] { "corpse_brigade", "lucavi", "northern_sky", "southern_sky" };

            // Act
            var orlandeauThemes = manager.GetAvailableThemes("Orlandeau");

            // Assert
            // Original should be included so players can revert to default
            orlandeauThemes.Should().Contain("original",
                "Orlandeau themes should include 'original' for reverting to default");

            // But other generic themes shouldn't be in the list
            foreach (var genericTheme in genericThemes)
            {
                orlandeauThemes.Should().NotContain(genericTheme,
                    $"Orlandeau themes should not include generic theme '{genericTheme}'");
            }
        }

        [Fact]
        public void OrlandeauThemes_Should_Have_Valid_Names()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();
            var expectedToContain = new[] { "original" };
            var optionalThemes = new[] { "thunder_god" };

            // Act
            var actualThemes = manager.GetAvailableThemes("Orlandeau");

            // Assert
            // Ensure required themes are present
            foreach (var requiredTheme in expectedToContain)
            {
                actualThemes.Should().Contain(requiredTheme,
                    $"Orlandeau should have '{requiredTheme}' theme");
            }

            // Verify all themes are non-empty strings
            actualThemes.Should().AllSatisfy(theme =>
                theme.Should().NotBeNullOrWhiteSpace("All theme names should be valid strings"));
        }

        [Fact]
        public void OrlandeauThemes_Should_Be_Accessible_Via_Manager()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();

            // Act
            var availableThemes = manager.GetAvailableThemes("Orlandeau");

            // Assert
            availableThemes.Should().NotBeEmpty("Orlandeau should have available themes");

            // Verify each theme can be set successfully
            foreach (var theme in availableThemes)
            {
                manager.SetCurrentTheme("Orlandeau", theme);
                var currentTheme = manager.GetCurrentTheme("Orlandeau");
                currentTheme.Should().Be(theme, $"Should be able to set and get theme '{theme}'");
            }
        }

        [Fact]
        public void StoryCharacterThemeManager_Should_Cycle_Orlandeau_Themes()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();
            var initialTheme = manager.GetCurrentTheme("Orlandeau");

            // Act
            var nextTheme = manager.CycleTheme("Orlandeau");

            // Assert
            nextTheme.Should().NotBe(initialTheme, "Cycling should change the theme");
        }

        [Fact]
        public void StoryCharacterThemeManager_Should_Wrap_Around_After_Last_Theme()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();
            var availableThemes = manager.GetAvailableThemes("Orlandeau");
            var themeCount = availableThemes.Count;

            // Act - Cycle through all themes
            for (int i = 0; i < themeCount; i++)
            {
                manager.CycleTheme("Orlandeau");
            }
            var wrappedTheme = manager.GetCurrentTheme("Orlandeau");

            // Assert
            // Should wrap back to first theme after cycling through all
            var firstTheme = availableThemes.First();
            wrappedTheme.Should().Be(firstTheme,
                "Should wrap back to first theme after cycling through all themes");
        }

        [Fact]
        public void Config_Should_Use_String_For_Orlandeau_Theme()
        {
            // Arrange
            var config = new Config();

            // Act
            var orlandeauTheme = config.Orlandeau;
            var orlandeauType = orlandeauTheme.GetType();

            // Assert
            orlandeauType.Should().Be(typeof(string),
                "Orlandeau configuration should use string themes");
            orlandeauTheme.Should().NotBeNullOrEmpty(
                "Orlandeau theme should have a default value");
        }

        [Fact]
        public void GetColorForSprite_Should_Return_Orlandeau_Theme_For_Oru_Sprite()
        {
            // Arrange
            var config = new Config();
            var manager = new StoryCharacterThemeManager();
            var availableThemes = manager.GetAvailableThemes("Orlandeau");

            // Use the second theme if available, otherwise original
            var testTheme = availableThemes.Count > 1 ? availableThemes[1] : "original";
            config.Orlandeau = testTheme;

            // Act
            var mapper = new SpriteNameMapper(config);
            var result = mapper.GetColorForSprite("battle_oru_spr.bin");

            // Assert
            if (testTheme == "original")
            {
                result.Should().Be("sprites_original",
                    "Should return original sprites for original theme");
            }
            else
            {
                result.Should().Be($"sprites_orlandeau_{testTheme}",
                    "Should return Orlandeau-specific theme directory");
            }
        }

    }
}