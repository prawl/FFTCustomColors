using Xunit;
using FluentAssertions;
using FFTColorMod;
using FFTColorMod.Configuration;
using System.ComponentModel;
using System.Linq;

namespace FFTColorMod.Tests
{
    public class OrlandeauThemeCyclingTests
    {
        [Fact]
        public void OrlandeauColorScheme_Should_Have_2_Themes()
        {
            // Arrange & Act
            var themes = System.Enum.GetValues(typeof(OrlandeauColorScheme));

            // Assert
            themes.Length.Should().Be(2, "Orlandeau should have exactly 2 themes: original and thunder_god");
        }

        [Fact]
        public void OrlandeauColorScheme_Should_Include_Original_But_Not_Other_Generic_Themes()
        {
            // Arrange
            var orlandeauThemes = System.Enum.GetNames(typeof(OrlandeauColorScheme));
            var genericThemes = new[] { "corpse_brigade", "lucavi", "northern_sky", "southern_sky" };

            // Act & Assert
            // Original should be included so players can revert to default
            orlandeauThemes.Should().Contain("original",
                "Orlandeau themes should include 'original' for reverting to default");

            // But other generic themes shouldn't be in the enum
            foreach (var genericTheme in genericThemes)
            {
                orlandeauThemes.Should().NotContain(genericTheme,
                    $"Orlandeau themes should not include generic theme '{genericTheme}'");
            }
        }

        [Fact]
        public void OrlandeauColorScheme_Should_Have_Descriptive_Names()
        {
            // Arrange
            var expectedThemes = new[]
            {
                "original",
                "thunder_god"
            };

            // Act
            var actualThemes = System.Enum.GetNames(typeof(OrlandeauColorScheme));

            // Assert
            actualThemes.Should().BeEquivalentTo(expectedThemes);
        }

        [Fact]
        public void OrlandeauColorScheme_Should_Have_Display_Descriptions()
        {
            // Arrange & Act
            var values = System.Enum.GetValues(typeof(OrlandeauColorScheme)).Cast<OrlandeauColorScheme>();

            // Assert
            foreach (var value in values)
            {
                var field = value.GetType().GetField(value.ToString());
                var attribute = field?.GetCustomAttributes(typeof(DescriptionAttribute), false)
                    .FirstOrDefault() as DescriptionAttribute;

                attribute.Should().NotBeNull($"Theme {value} should have a Description attribute");
                attribute?.Description.Should().NotBeNullOrWhiteSpace($"Theme {value} should have a non-empty description");
            }
        }

        [Fact]
        public void StoryCharacterThemeManager_Should_Cycle_Orlandeau_Themes()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();
            var initialTheme = manager.GetCurrentOrlandeauTheme();

            // Act
            var nextTheme = manager.CycleOrlandeauTheme();

            // Assert
            nextTheme.Should().NotBe(initialTheme, "Cycling should change the theme");
        }

        [Fact]
        public void StoryCharacterThemeManager_Should_Wrap_Around_After_Last_Theme()
        {
            // Arrange
            var manager = new StoryCharacterThemeManager();
            var themes = System.Enum.GetValues(typeof(OrlandeauColorScheme)).Length;

            // Act - Cycle through all themes
            for (int i = 0; i < themes; i++)
            {
                manager.CycleOrlandeauTheme();
            }
            var wrappedTheme = manager.GetCurrentOrlandeauTheme();

            // Assert
            wrappedTheme.Should().Be(OrlandeauColorScheme.thunder_god,
                "Should wrap back to first theme after cycling through all");
        }

        [Fact]
        public void Config_Should_Use_OrlandeauColorScheme_For_Orlandeau()
        {
            // Arrange
            var config = new Config();

            // Act
            var orlandeauType = config.Orlandeau.GetType();

            // Assert
            orlandeauType.Should().Be(typeof(OrlandeauColorScheme),
                "Orlandeau configuration should use OrlandeauColorScheme enum");
        }

        [Fact]
        public void GetColorForSprite_Should_Return_Orlandeau_Theme_For_Oru_Sprite()
        {
            // Arrange
            var config = new Config();
            config.Orlandeau = OrlandeauColorScheme.thunder_god;

            // Act
            var result = config.GetColorForSprite("battle_oru_spr.bin");

            // Assert
            result.Should().Be("sprites_orlandeau_thunder_god",
                "Should return Orlandeau-specific theme directory");
        }

    }
}