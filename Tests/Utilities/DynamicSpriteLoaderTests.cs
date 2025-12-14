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
        public void CleanupDataDirectory_Should_Preserve_Agrias_Themes()
        {
            // Arrange
            var agriasThemes = new[] {
                "sprites_agrias_holy_knight",
                "sprites_agrias_crimson_blade"
            };

            // Create Agrias theme directories
            foreach (var theme in agriasThemes)
            {
                var themeDir = Path.Combine(_testDataPath, theme);
                Directory.CreateDirectory(themeDir);
                File.WriteAllText(Path.Combine(themeDir, "battle_aguri_spr.bin"), "dummy");
            }

            var loader = new DynamicSpriteLoader(_testModPath, _configManager);
            var requiredSchemes = new HashSet<string> { "corpse_brigade" }; // Doesn't include Agrias

            // Act
            loader.CleanupDataDirectory(requiredSchemes);

            // Assert - Agrias themes should still exist
            foreach (var theme in agriasThemes)
            {
                var themeDir = Path.Combine(_testDataPath, theme);
                Directory.Exists(themeDir).Should().BeTrue(
                    $"{theme} should be preserved as a story character theme");
            }
        }

        [Fact]
        public void CleanupDataDirectory_Should_Preserve_Test_Themes()
        {
            // Arrange
            var testThemes = new[] {
                "sprites_test_custom",
                "sprites_test_special"
            };

            // Create test theme directories
            foreach (var theme in testThemes)
            {
                var themeDir = Path.Combine(_testDataPath, theme);
                Directory.CreateDirectory(themeDir);
                File.WriteAllText(Path.Combine(themeDir, "test.bin"), "dummy");
            }

            var loader = new DynamicSpriteLoader(_testModPath, _configManager);
            var requiredSchemes = new HashSet<string> { "corpse_brigade" }; // Doesn't include test themes

            // Act
            loader.CleanupDataDirectory(requiredSchemes);

            // Assert - Test themes should still exist
            foreach (var theme in testThemes)
            {
                var themeDir = Path.Combine(_testDataPath, theme);
                Directory.Exists(themeDir).Should().BeTrue(
                    $"{theme} should be preserved as a test theme");
            }
        }

        [Fact]
        public void CleanupDataDirectory_Should_Remove_Unused_Regular_Themes()
        {
            // Arrange
            var regularThemes = new[] {
                "sprites_lucavi",
                "sprites_northern_sky",
                "sprites_southern_sky"
            };

            // Create regular theme directories
            foreach (var theme in regularThemes)
            {
                var themeDir = Path.Combine(_testDataPath, theme);
                Directory.CreateDirectory(themeDir);
                File.WriteAllText(Path.Combine(themeDir, "test.bin"), "dummy");
            }

            // Also create the required theme
            var requiredThemeDir = Path.Combine(_testDataPath, "sprites_corpse_brigade");
            Directory.CreateDirectory(requiredThemeDir);
            File.WriteAllText(Path.Combine(requiredThemeDir, "test.bin"), "dummy");

            var loader = new DynamicSpriteLoader(_testModPath, _configManager);
            var requiredSchemes = new HashSet<string> { "corpse_brigade" }; // Only corpse_brigade is required

            // Act
            loader.CleanupDataDirectory(requiredSchemes);

            // Assert - Unused regular themes should be removed
            foreach (var theme in regularThemes)
            {
                var themeDir = Path.Combine(_testDataPath, theme);
                Directory.Exists(themeDir).Should().BeFalse(
                    $"{theme} should be removed as it's not required");
            }

            // But the required theme should still exist
            Directory.Exists(requiredThemeDir).Should().BeTrue(
                "sprites_corpse_brigade should be preserved as it's required");
        }

        [Fact]
        public void IsDevMode_Should_Always_Return_False()
        {
            // Arrange
            var loader = new DynamicSpriteLoader(_testModPath, _configManager);

            // Act & Assert
            loader.IsDevMode().Should().BeFalse("Dev mode has been removed");
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