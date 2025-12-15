using System;
using System.IO;
using Xunit;
using FFTColorMod.Configuration;
using FFTColorMod.Utilities;

namespace Tests
{
    public class ConfigBasedSpriteManagerTests : IDisposable
    {
        private string _testModPath;
        private string _testSourcePath;
        private ConfigurationManager _configManager;
        private ConfigBasedSpriteManager _spriteManager;

        public ConfigBasedSpriteManagerTests()
        {
            // Create temporary test directories
            _testModPath = Path.Combine(Path.GetTempPath(), "FFTColorModTest_" + Guid.NewGuid());
            _testSourcePath = Path.Combine(_testModPath, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(_testSourcePath);

            // Create the deployed mod path where sprites get copied to - match ConfigBasedSpriteManager expectations
            var deployedPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(deployedPath);

            // Setup config manager with test config - use proper config file path
            var configPath = Path.Combine(_testModPath, "Config.json");
            _configManager = new ConfigurationManager(configPath);

            // Create sprite manager - pass the source path's parent as third parameter (ColorMod directory)
            var sourceBasePath = Path.Combine(_testModPath, "ColorMod");
            _spriteManager = new ConfigBasedSpriteManager(_testModPath, _configManager, sourceBasePath);
        }

        public void Dispose()
        {
            // Clean up test directories
            if (Directory.Exists(_testModPath))
            {
                Directory.Delete(_testModPath, true);
            }
        }

        [Fact]
        public void ApplyConfiguration_WithGaffgarionBlacksteelRed_CopiesThemeFromDirectory()
        {
            // Arrange
            var config = new Config
            {
                Gaffgarion = GaffgarionColorScheme.blacksteel_red
            };
            _configManager.SaveConfig(config);

            // Create the theme directory structure
            var themeDir = Path.Combine(_testSourcePath, "sprites_gaffgarion_blacksteel_red");
            Directory.CreateDirectory(themeDir);

            // Create a dummy sprite file in the theme directory
            var themeSpriteFile = Path.Combine(themeDir, "battle_baruna_spr.bin");
            File.WriteAllText(themeSpriteFile, "blacksteel_red_theme_content");

            // Create the destination directory - match ConfigBasedSpriteManager expectations
            var destDir = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(destDir);

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert
            var expectedDestFile = Path.Combine(destDir, "battle_baruna_spr.bin");
            Assert.True(File.Exists(expectedDestFile), "Theme sprite should be copied to destination");

            var content = File.ReadAllText(expectedDestFile);
            Assert.Equal("blacksteel_red_theme_content", content);
        }

        [Fact]
        public void ApplyConfiguration_WithGaffgarionOriginal_DoesNotCopyTheme()
        {
            // Arrange
            var config = new Config
            {
                Gaffgarion = GaffgarionColorScheme.original
            };
            _configManager.SaveConfig(config);

            // Create the theme directory structure (shouldn't be used)
            var themeDir = Path.Combine(_testSourcePath, "sprites_gaffgarion_blacksteel_red");
            Directory.CreateDirectory(themeDir);

            // Create a dummy sprite file in the theme directory
            var themeSpriteFile = Path.Combine(themeDir, "battle_baruna_spr.bin");
            File.WriteAllText(themeSpriteFile, "blacksteel_red_theme_content");

            // Create the destination directory - match ConfigBasedSpriteManager expectations
            var destDir = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(destDir);

            // Create an existing original file
            var originalFile = Path.Combine(destDir, "battle_baruna_spr.bin");
            File.WriteAllText(originalFile, "original_content");

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert
            Assert.True(File.Exists(originalFile), "Original file should still exist");
            var content = File.ReadAllText(originalFile);
            Assert.Equal("original_content", content);
        }

        [Fact]
        public void ApplyConfiguration_WithMissingThemeDirectory_LogsWarning()
        {
            // Arrange
            var config = new Config
            {
                Gaffgarion = GaffgarionColorScheme.blacksteel_red
            };
            _configManager.SaveConfig(config);

            // Don't create the theme directory - it's missing

            // Create the destination directory - match ConfigBasedSpriteManager expectations
            var destDir = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(destDir);

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert
            // The sprite file should not be created since source doesn't exist
            var expectedDestFile = Path.Combine(destDir, "battle_baruna_spr.bin");
            Assert.False(File.Exists(expectedDestFile), "No file should be created when theme is missing");
        }

        [Fact]
        public void ApplyConfiguration_WithFlatFileStructure_CopiesThemeFromFlatFile()
        {
            // Arrange
            var config = new Config
            {
                Gaffgarion = GaffgarionColorScheme.blacksteel_red
            };
            _configManager.SaveConfig(config);

            // Create a flat file structure (backward compatibility)
            var flatThemeFile = Path.Combine(_testSourcePath, "battle_baruna_blacksteel_red_spr.bin");
            File.WriteAllText(flatThemeFile, "flat_blacksteel_red_content");

            // Create the destination directory - match ConfigBasedSpriteManager expectations
            var destDir = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(destDir);

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert
            var expectedDestFile = Path.Combine(destDir, "battle_baruna_spr.bin");
            Assert.True(File.Exists(expectedDestFile), "Theme sprite should be copied from flat file");

            var content = File.ReadAllText(expectedDestFile);
            Assert.Equal("flat_blacksteel_red_content", content);
        }

        [Fact]
        public void ApplyConfiguration_WithDirectoryAndFlatFile_PrefersDirectory()
        {
            // Arrange
            var config = new Config
            {
                Gaffgarion = GaffgarionColorScheme.blacksteel_red
            };
            _configManager.SaveConfig(config);

            // Create both directory structure and flat file
            var themeDir = Path.Combine(_testSourcePath, "sprites_gaffgarion_blacksteel_red");
            Directory.CreateDirectory(themeDir);

            var dirSpriteFile = Path.Combine(themeDir, "battle_baruna_spr.bin");
            File.WriteAllText(dirSpriteFile, "directory_content");

            var flatThemeFile = Path.Combine(_testSourcePath, "battle_baruna_blacksteel_red_spr.bin");
            File.WriteAllText(flatThemeFile, "flat_content");

            // Create the destination directory - match ConfigBasedSpriteManager expectations
            var destDir = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(destDir);

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert
            var expectedDestFile = Path.Combine(destDir, "battle_baruna_spr.bin");
            Assert.True(File.Exists(expectedDestFile), "Theme sprite should be copied");

            var content = File.ReadAllText(expectedDestFile);
            Assert.Equal("directory_content", content);
        }
    }
}