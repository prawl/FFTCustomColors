using System;
using System.IO;
using Xunit;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Utilities;
using FFTColorCustomizer.Services;

namespace Tests.Utilities
{
    public class ConfigBasedSpriteManagerTests : IDisposable
    {
        private string _testModPath;
        private string _testSourcePath;
        private ConfigurationManager _configManager;

        public ConfigBasedSpriteManagerTests()
        {
            // Reset the singleton to avoid test pollution
            CharacterServiceSingleton.Reset();

            // Create temporary test directories
            _testModPath = Path.Combine(Path.GetTempPath(), "FFTColorCustomizerTest_" + Guid.NewGuid());
            _testSourcePath = Path.Combine(_testModPath, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(_testSourcePath);

            // Create the deployed mod path where sprites get copied to
            var deployedPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(deployedPath);

            // Setup config manager with test config
            var configPath = Path.Combine(_testModPath, "Config.json");
            _configManager = new ConfigurationManager(configPath);
        }

        public void Dispose()
        {
            // Clean up test directories
            if (Directory.Exists(_testModPath))
            {
                Directory.Delete(_testModPath, true);
            }

            // Reset the singleton after tests
            CharacterServiceSingleton.Reset();
        }

        [Fact]
        public void ApplyConfiguration_Should_Use_CharacterDefinitionService()
        {
            // Arrange
            var service = new CharacterDefinitionService();
            service.AddCharacter(new CharacterDefinition
            {
                Name = "Agrias",
                SpriteNames = new[] { "aguri" },
                DefaultTheme = "original",
                AvailableThemes = new[] { "original", "ash_dark" },
                EnumType = "StoryCharacter"
            });

            var config = new Config
            {
                Agrias = "ash_dark"
            };
            _configManager.SaveConfig(config);

            // CRITICAL FIX: Create theme in deployed path, not source path
            // After the fix, ConfigBasedSpriteManager looks for themes in mod path
            var deployedPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var themeDir = Path.Combine(deployedPath, "sprites_agrias_ash_dark");
            Directory.CreateDirectory(themeDir);

            // Create a dummy sprite file in the theme directory
            var themeSpriteFile = Path.Combine(themeDir, "battle_aguri_spr.bin");
            File.WriteAllText(themeSpriteFile, "ash_dark_theme_content");

            // Create the destination directory
            var destDir = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(destDir);

            var sourceBasePath = Path.Combine(_testModPath, "ColorMod");
            var spriteManager = new ConfigBasedSpriteManager(_testModPath, _configManager, service, sourceBasePath);

            // Act
            spriteManager.ApplyConfiguration();

            // Assert
            var expectedDestFile = Path.Combine(destDir, "battle_aguri_spr.bin");
            Assert.True(File.Exists(expectedDestFile), "Theme sprite should be copied to destination");

            var content = File.ReadAllText(expectedDestFile);
            Assert.Equal("ash_dark_theme_content", content);
        }
    }
}
