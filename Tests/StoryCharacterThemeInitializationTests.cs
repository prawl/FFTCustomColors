using System;
using System.IO;
using Xunit;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer;
using FFTColorCustomizer.Services;

namespace Tests
{
    public class StoryCharacterThemeInitializationTests : IDisposable
    {
        private string _testModPath;
        private string _testSourcePath;
        private ConfigurationManager _configManager;
        private string _configPath;

        public StoryCharacterThemeInitializationTests()
        {
            // Create temporary test directories
            _testModPath = Path.Combine(Path.GetTempPath(), "FFTThemeTest_" + Guid.NewGuid());
            _testSourcePath = Path.Combine(_testModPath, "ColorMod");

            // Create necessary directories
            Directory.CreateDirectory(_testModPath);
            Directory.CreateDirectory(Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit"));
            Directory.CreateDirectory(Path.Combine(_testSourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit"));
            Directory.CreateDirectory(Path.Combine(_testSourcePath, "Data"));

            // Create theme directories for Agrias
            var agriasOriginalDir = Path.Combine(_testSourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "sprites_agrias_original");
            var agriasAshDir = Path.Combine(_testSourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "sprites_agrias_ash_dark");
            Directory.CreateDirectory(agriasOriginalDir);
            Directory.CreateDirectory(agriasAshDir);

            // Create dummy sprite files
            File.WriteAllText(Path.Combine(agriasOriginalDir, "battle_aguri_spr.bin"), "original_theme");
            File.WriteAllText(Path.Combine(agriasAshDir, "battle_aguri_spr.bin"), "ash_dark_theme");

            // Create theme directories for Cloud - only original is available
            var cloudOriginalDir = Path.Combine(_testSourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "sprites_cloud_original");
            Directory.CreateDirectory(cloudOriginalDir);

            File.WriteAllText(Path.Combine(cloudOriginalDir, "battle_cloud_spr.bin"), "original_theme");

            // Create StoryCharacters.json file
            var storyCharactersJson = @"{
  ""characters"": [
    {
      ""name"": ""Agrias"",
      ""spriteNames"": [""aguri"", ""kanba""],
      ""defaultTheme"": ""original"",
      ""availableThemes"": [""original"", ""ash_dark""]
    },
    {
      ""name"": ""Cloud"",
      ""spriteNames"": [""cloud""],
      ""defaultTheme"": ""original"",
      ""availableThemes"": [""original""]
    }
  ]
}";
            File.WriteAllText(Path.Combine(_testSourcePath, "Data", "StoryCharacters.json"), storyCharactersJson);

            // Setup config manager
            _configPath = Path.Combine(_testModPath, "Config.json");
            _configManager = new ConfigurationManager(_configPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testModPath))
            {
                Directory.Delete(_testModPath, true);
            }
        }

        [Fact]
        public void StoryCharacterThemes_OldBehavior_UsesHardcodedDefaults()
        {
            // This test demonstrates the OLD BUGGY behavior where themes were applied
            // before config was loaded, resulting in hardcoded defaults being used

            // Arrange
            var config = new Config
            {
                Agrias = "original",  // Config says original
                Cloud = "original",  // Only original is available for Cloud
                Orlandeau = "original"
            };
            _configManager.SaveConfig(config);

            // Act - Initialize theme manager and apply themes BEFORE setting from config
            var themeManager = new ThemeManager(_testSourcePath, _testModPath);

            // Apply themes with defaults (old behavior)
            themeManager.ApplyInitialThemes();

            // Then load config and set themes (too late - themes already copied)
            var storyManager = themeManager.GetStoryCharacterManager();
            storyManager.SetCurrentTheme("Agrias", config.Agrias);
            storyManager.SetCurrentTheme("Cloud", config.Cloud);

            // Check what was actually copied to deployment
            var deployedAgriasSprite = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_aguri_spr.bin");
            var deployedCloudSprite = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_cloud_spr.bin");

            // Assert - With the old behavior, deployed sprites would have hardcoded defaults
            Assert.True(File.Exists(deployedAgriasSprite), "Agrias sprite should be deployed");
            var agriasContent = File.ReadAllText(deployedAgriasSprite);

            // With defaults now set to "original", this should pass
            Assert.Equal("original_theme", agriasContent);

            Assert.True(File.Exists(deployedCloudSprite), "Cloud sprite should be deployed");
            var cloudContent = File.ReadAllText(deployedCloudSprite);
            Assert.Equal("original_theme", cloudContent); // Cloud would get original with old behavior
        }

        [Fact]
        public void StoryCharacterThemes_WhenAppliedAfterConfig_ShouldUseCorrectThemes()
        {
            // Arrange
            var config = new Config
            {
                Agrias = "ash_dark",
                Cloud = "original"
            };
            _configManager.SaveConfig(config);

            // Act - Initialize theme manager but DON'T apply themes yet
            var themeManager = new ThemeManager(_testSourcePath, _testModPath);
            var storyManager = themeManager.GetStoryCharacterManager();

            // Set themes from config FIRST
            storyManager.SetCurrentTheme("Agrias", config.Agrias);
            storyManager.SetCurrentTheme("Cloud", config.Cloud);

            // THEN apply themes
            themeManager.ApplyInitialThemes();

            // Check what was deployed
            var deployedAgriasSprite = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_aguri_spr.bin");
            var deployedCloudSprite = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_cloud_spr.bin");

            // Assert - Now themes should match config
            Assert.True(File.Exists(deployedAgriasSprite), "Agrias sprite should be deployed");
            var agriasContent = File.ReadAllText(deployedAgriasSprite);
            Assert.Equal("ash_dark_theme", agriasContent); // Should pass when themes are set before applying

            Assert.True(File.Exists(deployedCloudSprite), "Cloud sprite should be deployed");
            var cloudContent = File.ReadAllText(deployedCloudSprite);
            Assert.Equal("original_theme", cloudContent);
        }
    }
}
