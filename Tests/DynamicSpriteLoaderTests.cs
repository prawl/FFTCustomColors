using Xunit;
using FluentAssertions;
using FFTColorMod.Utilities;
using FFTColorMod.Configuration;
using System.IO;
using System;
using System.Collections.Generic;

namespace FFTColorMod.Tests
{
    public class DynamicSpriteLoaderTests : IDisposable
    {
        private readonly string _testModPath;
        private readonly string _testDataPath;
        private readonly ConfigurationManager _configManager;

        public DynamicSpriteLoaderTests()
        {
            // Create a temporary test directory
            _testModPath = Path.Combine(Path.GetTempPath(), $"FFTColorModTest_{Guid.NewGuid()}");
            _testDataPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(_testDataPath);

            // Create a mock config manager
            var configPath = Path.Combine(_testModPath, "Config.json");
            _configManager = new ConfigurationManager(configPath);
        }

        [Fact]
        public void PrepareSpritesForConfig_Should_Not_Delete_CoreDevThemes_In_DevMode()
        {
            // Arrange
            var coreThemes = new[] {
                "sprites_corpse_brigade",
                "sprites_lucavi",
                "sprites_northern_sky",
                "sprites_southern_sky"
            };

            // Create the core theme directories
            foreach (var theme in coreThemes)
            {
                var themeDir = Path.Combine(_testDataPath, theme);
                Directory.CreateDirectory(themeDir);
                // Add a dummy file to make it non-empty
                File.WriteAllText(Path.Combine(themeDir, "test.bin"), "dummy");
            }

            var loader = new DynamicSpriteLoader(_testModPath, _configManager, isDevMode: true);

            // Act
            loader.PrepareSpritesForConfig();

            // Assert
            foreach (var theme in coreThemes)
            {
                var themeDir = Path.Combine(_testDataPath, theme);
                Directory.Exists(themeDir).Should().BeTrue(
                    $"{theme} should not be deleted in dev mode");
            }
        }

        [Fact]
        public void PrepareSpritesForConfig_Should_Not_Delete_Orlandeau_Themes_In_DevMode()
        {
            // Arrange
            var orlandeauThemes = new[] {
                "sprites_orlandeau_thunder_god",
                "sprites_orlandeau_crimson_knight",
                "sprites_orlandeau_shadow_lord",
                "sprites_orlandeau_holy_paladin"
            };

            // Create the Orlandeau theme directories
            foreach (var theme in orlandeauThemes)
            {
                var themeDir = Path.Combine(_testDataPath, theme);
                Directory.CreateDirectory(themeDir);
                File.WriteAllText(Path.Combine(themeDir, "battle_oru_spr.bin"), "dummy");
            }

            var loader = new DynamicSpriteLoader(_testModPath, _configManager, isDevMode: true);

            // Act
            loader.PrepareSpritesForConfig();

            // Assert
            foreach (var theme in orlandeauThemes)
            {
                var themeDir = Path.Combine(_testDataPath, theme);
                Directory.Exists(themeDir).Should().BeTrue(
                    $"{theme} should not be deleted in dev mode");
            }
        }

        [Fact]
        public void CleanupDataDirectory_Should_Preserve_Orlandeau_Themes()
        {
            // Arrange
            var orlandeauThemes = new[] {
                "sprites_orlandeau_thunder_god",
                "sprites_orlandeau_crimson_knight"
            };

            // Create Orlandeau theme directories
            foreach (var theme in orlandeauThemes)
            {
                var themeDir = Path.Combine(_testDataPath, theme);
                Directory.CreateDirectory(themeDir);
                File.WriteAllText(Path.Combine(themeDir, "battle_oru_spr.bin"), "dummy");
            }

            var loader = new DynamicSpriteLoader(_testModPath, _configManager);
            var requiredSchemes = new HashSet<string> { "corpse_brigade" }; // Doesn't include Orlandeau

            // Act
            loader.CleanupDataDirectory(requiredSchemes);

            // Assert - Orlandeau themes should still exist
            foreach (var theme in orlandeauThemes)
            {
                var themeDir = Path.Combine(_testDataPath, theme);
                Directory.Exists(themeDir).Should().BeTrue(
                    $"{theme} should be preserved as a story character theme");
            }
        }

        [Fact]
        public void GetRequiredSchemes_Should_Include_F2_Cycling_Themes_In_DevMode()
        {
            // Arrange
            var loader = new DynamicSpriteLoader(_testModPath, _configManager, isDevMode: true);

            // Act - This would need to be made public or tested via PrepareSpritesForConfig
            // For now, we'll test the behavior indirectly
            var coreThemes = new[] {
                "sprites_corpse_brigade",
                "sprites_lucavi",
                "sprites_northern_sky",
                "sprites_southern_sky"
            };

            foreach (var theme in coreThemes)
            {
                var themeDir = Path.Combine(_testDataPath, theme);
                Directory.CreateDirectory(themeDir);
            }

            loader.PrepareSpritesForConfig();

            // Assert
            foreach (var theme in coreThemes)
            {
                Directory.Exists(Path.Combine(_testDataPath, theme)).Should().BeTrue();
            }
        }

        [Fact]
        public void DetectDevMode_Should_Return_True_When_CoreThemesAndOrlandeauThemesPresent()
        {
            // Arrange
            // Create core dev themes
            var coreThemes = new[] { "sprites_corpse_brigade", "sprites_lucavi", "sprites_northern_sky", "sprites_southern_sky" };
            foreach (var theme in coreThemes)
            {
                var themeDir = Path.Combine(_testDataPath, theme);
                Directory.CreateDirectory(themeDir);
                File.WriteAllText(Path.Combine(themeDir, "test.bin"), "dummy");
            }

            // Create Orlandeau themes
            var orlandeauThemes = new[] { "sprites_orlandeau_thunder_god", "sprites_orlandeau_crimson_knight" };
            foreach (var theme in orlandeauThemes)
            {
                var themeDir = Path.Combine(_testDataPath, theme);
                Directory.CreateDirectory(themeDir);
                File.WriteAllText(Path.Combine(themeDir, "battle_oru_spr.bin"), "dummy");
            }

            // Act - Create loader without forcing dev mode, let it auto-detect
            var loader = new DynamicSpriteLoader(_testModPath, _configManager);

            // Assert
            loader.IsDevMode().Should().BeTrue("Dev mode should be detected when core themes are present, even with Orlandeau themes");
        }

        public void Dispose()
        {
            // Clean up test directory
            if (Directory.Exists(_testModPath))
            {
                Directory.Delete(_testModPath, true);
            }
        }
    }
}