using Xunit;
using FluentAssertions;
using FFTColorMod;
using FFTColorMod.Configuration;
using System.IO;
using System;

namespace FFTColorMod.Tests
{
    public class F2SimultaneousCyclingTests
    {
        [Fact]
        public void F2_Should_Cycle_Both_Generic_And_Orlandeau_Themes_Simultaneously()
        {
            // Arrange
            var handler = new F2ThemeHandler();
            var initialGenericTheme = handler.GetCurrentGenericTheme();
            var initialOrlandeauTheme = handler.GetCurrentOrlandeauTheme();

            // Act
            handler.HandleF2Press();
            var newGenericTheme = handler.GetCurrentGenericTheme();
            var newOrlandeauTheme = handler.GetCurrentOrlandeauTheme();

            // Assert
            newGenericTheme.Should().NotBe(initialGenericTheme,
                "Generic theme should advance when F2 is pressed");
            newOrlandeauTheme.Should().NotBe(initialOrlandeauTheme,
                "Orlandeau theme should advance when F2 is pressed");
        }

        [Fact]
        public void F2_Should_Cycle_Generic_From_Original_To_CorpseBrigade()
        {
            // Arrange
            var handler = new F2ThemeHandler();
            handler.SetGenericTheme("original");

            // Act
            handler.HandleF2Press();
            var genericTheme = handler.GetCurrentGenericTheme();

            // Assert
            genericTheme.Should().Be("corpse_brigade",
                "Generic themes should cycle from original to corpse_brigade");
        }

        [Fact]
        public void F2_Should_Cycle_Orlandeau_From_ThunderGod_To_CrimsonKnight()
        {
            // Arrange
            var handler = new F2ThemeHandler();
            handler.SetOrlandeauTheme(OrlandeauColorScheme.thunder_god);

            // Act
            handler.HandleF2Press();
            var orlandeauTheme = handler.GetCurrentOrlandeauTheme();

            // Assert
            orlandeauTheme.Should().Be(OrlandeauColorScheme.original,
                "Orlandeau themes should cycle from thunder_god to original");
        }

        [Fact]
        public void F2_Should_Apply_Correct_Sprite_Files_For_Both_Themes()
        {
            // Arrange
            var handler = new F2ThemeHandler();
            handler.SetGenericTheme("original");
            handler.SetOrlandeauTheme(OrlandeauColorScheme.thunder_god);

            // Act
            handler.HandleF2Press();
            var knightSpritePath = handler.GetSpritePathFor("battle_knight_m_spr.bin");
            var orlandeauSpritePath = handler.GetSpritePathFor("battle_oru_spr.bin");

            // Assert
            knightSpritePath.Should().Be("sprites_corpse_brigade/battle_knight_m_spr.bin",
                "Knight sprite should use corpse_brigade theme after F2");
            orlandeauSpritePath.Should().Be("sprites_orlandeau_original/battle_oru_spr.bin",
                "Orlandeau sprite should use original theme after F2 (cycles from thunder_god to original)");
        }

        [Fact]
        public void F2_Should_Wrap_Around_Both_Theme_Lists_Independently()
        {
            // Arrange
            var handler = new F2ThemeHandler();

            // Set to last themes
            handler.SetGenericTheme("amethyst"); // Last generic theme
            handler.SetOrlandeauTheme(OrlandeauColorScheme.thunder_god); // Last Orlandeau theme (only 2: original and thunder_god)

            // Act
            handler.HandleF2Press();
            var genericTheme = handler.GetCurrentGenericTheme();
            var orlandeauTheme = handler.GetCurrentOrlandeauTheme();

            // Assert
            genericTheme.Should().Be("original",
                "Generic themes should wrap to original after last theme");
            orlandeauTheme.Should().Be(OrlandeauColorScheme.original,
                "Orlandeau themes should wrap to original after thunder_god");
        }

        [Fact]
        public void Multiple_F2_Presses_Should_Cycle_Both_Themes_Correctly()
        {
            // Arrange
            var handler = new F2ThemeHandler();
            handler.SetGenericTheme("original");
            handler.SetOrlandeauTheme(OrlandeauColorScheme.thunder_god);

            // Act - Press F2 three times
            handler.HandleF2Press();
            handler.HandleF2Press();
            handler.HandleF2Press();

            var genericTheme = handler.GetCurrentGenericTheme();
            var orlandeauTheme = handler.GetCurrentOrlandeauTheme();

            // Assert
            genericTheme.Should().Be("northern_sky",
                "After 3 presses: original -> corpse_brigade -> lucavi -> northern_sky");
            orlandeauTheme.Should().Be(OrlandeauColorScheme.original,
                "After 3 presses with only 2 themes: thunder_god -> original -> thunder_god -> original");
        }

        [Fact]
        public void F2_Handler_Should_Log_Both_Theme_Changes()
        {
            // Arrange
            var handler = new F2ThemeHandler();
            var logMessages = new System.Collections.Generic.List<string>();
            handler.OnLog = message => logMessages.Add(message);

            // Act
            handler.HandleF2Press();

            // Assert
            logMessages.Should().Contain(msg => msg.Contains("Generic"),
                "Should log generic theme change");
            logMessages.Should().Contain(msg => msg.Contains("Orlandeau"),
                "Should log Orlandeau theme change");
        }
    }

    // Test implementation class
    public class F2ThemeHandler
    {
        private readonly ColorSchemeCycler _genericCycler;
        private readonly StoryCharacterThemeManager _storyManager;
        private string _currentGenericTheme = "original";
        private OrlandeauColorScheme _currentOrlandeauTheme = OrlandeauColorScheme.thunder_god;

        public Action<string> OnLog { get; set; }

        public F2ThemeHandler()
        {
            _genericCycler = new ColorSchemeCycler();
            _storyManager = new StoryCharacterThemeManager();
        }

        public void HandleF2Press()
        {
            // Cycle generic theme
            _currentGenericTheme = _genericCycler.GetNextScheme();
            OnLog?.Invoke($"[Generic] Cycling to {_currentGenericTheme}");

            // Cycle Orlandeau theme
            _currentOrlandeauTheme = _storyManager.CycleOrlandeauTheme();
            OnLog?.Invoke($"[Orlandeau] Cycling to {_currentOrlandeauTheme}");
        }

        public string GetCurrentGenericTheme() => _currentGenericTheme;
        public OrlandeauColorScheme GetCurrentOrlandeauTheme() => _currentOrlandeauTheme;

        public void SetGenericTheme(string theme)
        {
            _currentGenericTheme = theme;
            _genericCycler.SetCurrentScheme(theme);
        }

        public void SetOrlandeauTheme(OrlandeauColorScheme theme)
        {
            _currentOrlandeauTheme = theme;
            _storyManager.SetCurrentOrlandeauTheme(theme);
        }

        public string GetSpritePathFor(string spriteName)
        {
            if (spriteName.Contains("oru"))
            {
                return $"sprites_orlandeau_{_currentOrlandeauTheme.ToString().ToLower()}/{spriteName}";
            }
            else
            {
                return $"sprites_{_currentGenericTheme}/{spriteName}";
            }
        }
    }
}