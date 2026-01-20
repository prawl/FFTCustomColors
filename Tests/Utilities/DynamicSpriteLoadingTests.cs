using System;
using System.IO;
using Xunit;
using FFTColorCustomizer.Utilities;
using FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Tests
{
    public class DynamicSpriteLoadingTests : IDisposable
    {
        private readonly string _testModPath;
        private readonly string _dataPath;
        private readonly ConfigurationManager _configManager;
        private readonly DynamicSpriteLoader _loader;

        public DynamicSpriteLoadingTests()
        {
            _testModPath = Path.Combine(Path.GetTempPath(), "FFTColorCustomizerTest_" + Guid.NewGuid());
            _dataPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

            Directory.CreateDirectory(_dataPath);

            var configPath = Path.Combine(_testModPath, "Config.json");
            _configManager = new ConfigurationManager(configPath);
            _loader = new DynamicSpriteLoader(_testModPath, _configManager);

            SetupTestSprites();
        }

        private void SetupTestSprites()
        {
            // Create test sprite files directly in data directory
            var schemes = new[] { "sprites_original", "sprites_corpse_brigade", "sprites_lucavi", "sprites_crimson_red", "sprites_golden_templar" };
            var sprites = new[] { "battle_knight_m_spr.bin", "battle_knight_w_spr.bin", "battle_monk_m_spr.bin" };

            foreach (var scheme in schemes)
            {
                var schemeDir = Path.Combine(_dataPath, scheme);
                Directory.CreateDirectory(schemeDir);

                foreach (var sprite in sprites)
                {
                    var spritePath = Path.Combine(schemeDir, sprite);
                    File.WriteAllBytes(spritePath, new byte[] { 1, 2, 3 }); // Dummy content
                }
            }

            // Also create sprites_original in data path as the base
            var originalDir = Path.Combine(_dataPath, "sprites_original");
            Directory.CreateDirectory(originalDir);
            foreach (var sprite in sprites)
            {
                var spritePath = Path.Combine(originalDir, sprite);
                File.WriteAllBytes(spritePath, new byte[] { 1, 2, 3 });
            }
        }

        [Fact]
        public void PrepareSpritesForConfig_WithSingleScheme_CopiesOnlyRequiredSprites()
        {
            // Arrange
            var config = new Config();
            config["Knight_Male"] = "corpse_brigade";
            config["Knight_Female"] = "corpse_brigade";
            // All other jobs remain as original
            _configManager.SaveConfig(config);

            // Act
            _loader.PrepareSpritesForConfig();

            // Assert
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_corpse_brigade")));
            Assert.False(Directory.Exists(Path.Combine(_dataPath, "sprites_lucavi")));
            Assert.False(Directory.Exists(Path.Combine(_dataPath, "sprites_crimson_red")));

            // Should have copied corpse_brigade sprites
            Assert.True(File.Exists(Path.Combine(_dataPath, "sprites_corpse_brigade", "battle_knight_m_spr.bin")));
            Assert.True(File.Exists(Path.Combine(_dataPath, "sprites_corpse_brigade", "battle_knight_w_spr.bin")));
        }

        [Fact]
        public void PrepareSpritesForConfig_WithMultipleSchemes_CopiesAllRequired()
        {
            // Arrange
            var config = new Config();
            config["Knight_Male"] = "corpse_brigade";
            config["Knight_Female"] = "lucavi";
            config["Monk_Male"] = "original";
            _configManager.SaveConfig(config);

            // Act
            _loader.PrepareSpritesForConfig();

            // Assert
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_corpse_brigade")));
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_lucavi")));
            Assert.False(Directory.Exists(Path.Combine(_dataPath, "sprites_crimson_red")));
            Assert.False(Directory.Exists(Path.Combine(_dataPath, "sprites_golden_templar")));
        }

        [Fact]
        public void PrepareSpritesForConfig_RemovesUnusedSchemes()
        {
            // Arrange - First add some schemes
            var initialConfig = new Config();
            initialConfig["Knight_Male"] = "corpse_brigade";
            initialConfig["Monk_Male"] = "lucavi";
            _configManager.SaveConfig(initialConfig);
            _loader.PrepareSpritesForConfig();

            // Verify initial state
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_corpse_brigade")));
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_lucavi")));

            // Now change config to only use corpse_brigade
            var newConfig = new Config();
            newConfig["Knight_Male"] = "corpse_brigade";
            // All others are original
            _configManager.SaveConfig(newConfig);

            // Act
            _loader.PrepareSpritesForConfig();

            // Assert - lucavi should be removed
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_corpse_brigade")));
            Assert.False(Directory.Exists(Path.Combine(_dataPath, "sprites_lucavi")));
        }

        [Fact]
        public void PrepareSpritesForConfig_AlwaysKeepsOriginal()
        {
            // Arrange
            var config = new Config();
            config["Knight_Male"] = "corpse_brigade";
            _configManager.SaveConfig(config);

            // Act
            _loader.PrepareSpritesForConfig();

            // Assert - original should always exist
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_original")));
        }

        [Fact]
        public void PrepareSpritesForConfig_HandlesAllOriginalConfig()
        {
            // Arrange - All jobs set to original (default)
            var config = new Config();
            _configManager.SaveConfig(config);

            // Act
            _loader.PrepareSpritesForConfig();

            // Assert - only original should exist
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_original")));
            Assert.False(Directory.Exists(Path.Combine(_dataPath, "sprites_corpse_brigade")));
            Assert.False(Directory.Exists(Path.Combine(_dataPath, "sprites_lucavi")));
        }

        [Fact]
        public void GetRequiredSchemes_ReturnsUniqueSchemes()
        {
            // Arrange
            var config = new Config();
            config["Knight_Male"] = "corpse_brigade";
            config["Knight_Female"] = "corpse_brigade";
            config["Monk_Male"] = "lucavi";
            config["Monk_Female"] = "lucavi";
            _configManager.SaveConfig(config);

            // Debug: verify the config was saved correctly
            var loadedConfig = _configManager.LoadConfig();
            Assert.Equal("corpse_brigade", loadedConfig["Knight_Male"]);
            Assert.Equal("lucavi", loadedConfig["Monk_Male"]);

            // Act
            var schemes = _loader.GetRequiredSchemes();

            // Assert
            Assert.Contains("original", schemes);
            Assert.Contains("corpse_brigade", schemes);
            Assert.Contains("lucavi", schemes);
            Assert.Equal(3, schemes.Count);
        }

        [Fact]
        public void PrepareSpritesForConfig_PreservesTestThemes()
        {
            // Arrange - Create a test theme in data
            var testThemeDir = Path.Combine(_dataPath, "sprites_test_custom");
            Directory.CreateDirectory(testThemeDir);
            File.WriteAllBytes(Path.Combine(testThemeDir, "test.bin"), new byte[] { 1 });

            var config = new Config();
            config["Knight_Male"] = "corpse_brigade";
            _configManager.SaveConfig(config);

            // Act
            _loader.PrepareSpritesForConfig();

            // Assert - test theme should still exist
            Assert.True(Directory.Exists(testThemeDir));
            Assert.True(File.Exists(Path.Combine(testThemeDir, "test.bin")));
        }

        [Fact]
        public void CleanupDataDirectory_RemovesOnlyUnusedThemes()
        {
            // Arrange - Create some themes in data directory
            Directory.CreateDirectory(Path.Combine(_dataPath, "sprites_corpse_brigade"));
            Directory.CreateDirectory(Path.Combine(_dataPath, "sprites_lucavi"));
            Directory.CreateDirectory(Path.Combine(_dataPath, "sprites_golden_templar"));
            Directory.CreateDirectory(Path.Combine(_dataPath, "sprites_test_custom"));

            var requiredSchemes = new HashSet<string> { "original", "corpse_brigade" };

            // Act
            _loader.CleanupDataDirectory(requiredSchemes);

            // Assert
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_original")));
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_corpse_brigade")));
            Assert.True(Directory.Exists(Path.Combine(_dataPath, "sprites_test_custom"))); // Test themes preserved
            Assert.False(Directory.Exists(Path.Combine(_dataPath, "sprites_lucavi")));
            Assert.False(Directory.Exists(Path.Combine(_dataPath, "sprites_golden_templar")));
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
                // Ignore cleanup errors in tests
            }
        }
    }
}
