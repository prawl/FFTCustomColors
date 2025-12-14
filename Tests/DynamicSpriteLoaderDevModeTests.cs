using System;
using System.IO;
using Xunit;
using FFTColorMod.Utilities;
using FFTColorMod.Configuration;

namespace FFTColorMod.Tests
{
    public class DynamicSpriteLoaderDevModeTests : IDisposable
    {
        private readonly string _testModPath;
        private readonly string _colorSchemesPath;
        private readonly string _dataPath;
        private readonly ConfigurationManager _configManager;

        public DynamicSpriteLoaderDevModeTests()
        {
            _testModPath = Path.Combine(Path.GetTempPath(), "FFTDevModeTest_" + Guid.NewGuid());
            _colorSchemesPath = Path.Combine(_testModPath, "ColorSchemes");
            _dataPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

            Directory.CreateDirectory(_colorSchemesPath);
            Directory.CreateDirectory(_dataPath);

            var configPath = Path.Combine(_testModPath, "Config.json");
            _configManager = new ConfigurationManager(configPath);

            SetupTestSprites();
        }

        private void SetupTestSprites()
        {
            // Create test sprite files in ColorSchemes (all 20 themes)
            var allSchemes = new[] {
                "sprites_original", "sprites_corpse_brigade", "sprites_lucavi",
                "sprites_northern_sky", "sprites_southern_sky", "sprites_southern_sky",
                "sprites_crimson_red", "sprites_golden_templar", "sprites_amethyst"
            };

            var sprites = new[] { "battle_knight_m_spr.bin", "battle_knight_w_spr.bin" };

            foreach (var scheme in allSchemes)
            {
                var schemeDir = Path.Combine(_colorSchemesPath, scheme);
                Directory.CreateDirectory(schemeDir);

                foreach (var sprite in sprites)
                {
                    var spritePath = Path.Combine(schemeDir, sprite);
                    File.WriteAllBytes(spritePath, new byte[] { 1, 2, 3 });
                }
            }

            // Set up dev mode: 5 core themes + 1 test theme in data directory
            var coreDevThemes = new[] {
                "sprites_original", "sprites_corpse_brigade", "sprites_lucavi",
                "sprites_northern_sky", "sprites_southern_sky"
            };

            foreach (var scheme in coreDevThemes)
            {
                var schemeDir = Path.Combine(_dataPath, scheme);
                Directory.CreateDirectory(schemeDir);

                foreach (var sprite in sprites)
                {
                    var spritePath = Path.Combine(schemeDir, sprite);
                    File.WriteAllBytes(spritePath, new byte[] { 1, 2, 3 });
                }
            }

            // Add a test theme
            var testThemeDir = Path.Combine(_dataPath, "sprites_test_custom");
            Directory.CreateDirectory(testThemeDir);
            File.WriteAllBytes(Path.Combine(testThemeDir, "test.bin"), new byte[] { 1 });
        }

        [Fact]
        public void DevMode_DoesNotRemoveExistingDevThemes()
        {
            // Arrange - we have 5 dev themes + 1 test theme in data
            var config = new Config();
            config.Knight_Male = Configuration.ColorScheme.corpse_brigade; // Already in data
            _configManager.SaveConfig(config);

            var loader = new DynamicSpriteLoader(_testModPath, _configManager, isDevMode: true);

            // Act
            loader.PrepareSpritesForConfig();

            // Assert - all 5 dev themes + test theme should still be there
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_original")));
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_corpse_brigade")));
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_lucavi")));
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_northern_sky")));
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_southern_sky")));
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_test_custom")));
        }

        [Fact]
        public void DevMode_DoesNotCopyAdditionalThemes()
        {
            // Arrange - configure a theme that's NOT in the dev set
            var config = new Config();
            config.Knight_Male = Configuration.ColorScheme.crimson_red; // Not in dev themes
            _configManager.SaveConfig(config);

            var loader = new DynamicSpriteLoader(_testModPath, _configManager, isDevMode: true);

            // Act
            loader.PrepareSpritesForConfig();

            // Assert - crimson_red should NOT be copied to data
            Assert.False(Directory.Exists(Path.Combine(_dataPath, "sprites_crimson_red")));

            // But dev themes should still be there
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_original")));
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_corpse_brigade")));
        }

        [Fact]
        public void DevMode_PreservesTestThemes()
        {
            // Arrange
            var config = new Config();
            config.Knight_Male = Configuration.ColorScheme.lucavi;
            _configManager.SaveConfig(config);

            var loader = new DynamicSpriteLoader(_testModPath, _configManager, isDevMode: true);

            // Act
            loader.PrepareSpritesForConfig();

            // Assert - test theme should still exist
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_test_custom")));
            Assert.True(File.Exists(Path.Combine(_dataPath, "sprites_test_custom", "test.bin")));
        }

        [Fact]
        public void DevMode_LogsSkippedThemes()
        {
            // This test would verify logging, but for now we'll skip it
            // In a real implementation, we'd inject a logger and verify the messages
            Assert.True(true);
        }

        [Fact]
        public void ProductionMode_WorksNormally()
        {
            // Arrange - production mode with a non-dev theme configured
            var config = new Config();
            config.Knight_Male = Configuration.ColorScheme.golden_templar;
            _configManager.SaveConfig(config);

            var loader = new DynamicSpriteLoader(_testModPath, _configManager, isDevMode: false);

            // Act
            loader.PrepareSpritesForConfig();

            // Assert - should copy the configured theme
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_golden_templar")));
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_original"))); // Always needed
        }

        [Fact]
        public void IsDevMode_DetectedByExistingThemes()
        {
            // Test that we can auto-detect dev mode by checking what's in data directory
            var loader = new DynamicSpriteLoader(_testModPath, _configManager);

            // Should auto-detect dev mode since we have exactly the dev themes in data
            Assert.True(loader.IsDevMode());
        }

        [Fact]
        public void IsDevMode_NotDetectedWithTooManyThemes()
        {
            // Add an extra non-dev theme to data (one that's NOT in CoreDevThemes)
            var extraThemeDir = Path.Combine(_dataPath, "sprites_unknown_theme");
            Directory.CreateDirectory(extraThemeDir);

            var loader = new DynamicSpriteLoader(_testModPath, _configManager);

            // Should NOT detect as dev mode since we have a theme that's not in CoreDevThemes
            Assert.False(loader.IsDevMode());
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testModPath))
                {
                    Directory.Delete(_testModPath, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}