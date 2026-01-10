using Xunit;
using FluentAssertions;
using FFTColorCustomizer.Utilities;
using FFTColorCustomizer.Configuration;
using System.IO;
using System;
using System.Collections.Generic;

namespace FFTColorCustomizer.Tests
{
    public class DynamicSpriteLoaderTests : IDisposable
    {
        private readonly string _testModPath;
        private readonly string _testDataPath;
        private readonly ConfigurationManager _configManager;

        public DynamicSpriteLoaderTests()
        {
            // Create a temporary test directory
            _testModPath = Path.Combine(Path.GetTempPath(), $"FFTColorCustomizerTest_{Guid.NewGuid()}");
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

        [Theory]
        [InlineData("DarkKnight_Male", true)]
        [InlineData("DarkKnight_Female", true)]
        [InlineData("OnionKnight_Male", true)]
        [InlineData("OnionKnight_Female", true)]
        [InlineData("Knight_Male", false)]
        [InlineData("Knight_Female", false)]
        [InlineData("Squire_Male", false)]
        [InlineData("Archer_Female", false)]
        public void IsWotLJob_Should_Return_Correct_Value(string jobProperty, bool expectedIsWotL)
        {
            // Arrange
            var loader = new DynamicSpriteLoader(_testModPath, _configManager);

            // Act
            var result = loader.IsWotLJob(jobProperty);

            // Assert
            result.Should().Be(expectedIsWotL);
        }

        [Theory]
        [InlineData("DarkKnight_Male", "unit_psp")]
        [InlineData("DarkKnight_Female", "unit_psp")]
        [InlineData("OnionKnight_Male", "unit_psp")]
        [InlineData("OnionKnight_Female", "unit_psp")]
        [InlineData("Knight_Male", "unit")]
        [InlineData("Knight_Female", "unit")]
        [InlineData("Squire_Male", "unit")]
        [InlineData("Archer_Female", "unit")]
        public void GetUnitDirectory_Should_Return_Correct_Path(string jobProperty, string expectedUnitDir)
        {
            // Arrange
            var loader = new DynamicSpriteLoader(_testModPath, _configManager);

            // Act
            var result = loader.GetUnitDirectory(jobProperty);

            // Assert
            result.Should().EndWith(expectedUnitDir);
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
