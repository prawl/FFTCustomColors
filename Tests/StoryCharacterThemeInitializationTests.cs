using System;
using System.IO;
using Xunit;
using FFTColorMod.Configuration;
using FFTColorMod;
using FFTColorMod.Services;

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

            // Create theme directories for Agrias
            var agriasOriginalDir = Path.Combine(_testSourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "sprites_agrias_original");
            var agriasAshDir = Path.Combine(_testSourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "sprites_agrias_ash_dark");
            Directory.CreateDirectory(agriasOriginalDir);
            Directory.CreateDirectory(agriasAshDir);

            // Create dummy sprite files
            File.WriteAllText(Path.Combine(agriasOriginalDir, "battle_aguri_spr.bin"), "original_theme");
            File.WriteAllText(Path.Combine(agriasAshDir, "battle_aguri_spr.bin"), "ash_dark_theme");

            // Create theme directories for Gaffgarion
            var gaffOriginalDir = Path.Combine(_testSourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "sprites_gaffgarion_original");
            var gaffBlacksteelDir = Path.Combine(_testSourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "sprites_gaffgarion_blacksteel_red");
            Directory.CreateDirectory(gaffOriginalDir);
            Directory.CreateDirectory(gaffBlacksteelDir);

            File.WriteAllText(Path.Combine(gaffOriginalDir, "battle_baruna_spr.bin"), "original_theme");
            File.WriteAllText(Path.Combine(gaffBlacksteelDir, "battle_baruna_spr.bin"), "blacksteel_red_theme");

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
                Agrias = AgriasColorScheme.original,  // Config says original
                Gaffgarion = GaffgarionColorScheme.blacksteel_red,  // Config says blacksteel_red
                Orlandeau = OrlandeauColorScheme.original
            };
            _configManager.SaveConfig(config);

            // Act - Initialize theme manager and apply themes BEFORE setting from config
            var themeManager = new ThemeManager(_testSourcePath, _testModPath);

            // Apply themes with defaults (old behavior)
            themeManager.ApplyInitialThemes();

            // Then load config and set themes (too late - themes already copied)
            var storyManager = themeManager.GetStoryCharacterManager();
            storyManager.SetCurrentAgriasTheme(config.Agrias);
            storyManager.SetCurrentGaffgarionTheme(config.Gaffgarion);

            // Check what was actually copied to deployment
            var deployedAgriasSprite = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_aguri_spr.bin");
            var deployedGaffSprite = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_baruna_spr.bin");

            // Assert - With the old behavior, deployed sprites would have hardcoded defaults
            Assert.True(File.Exists(deployedAgriasSprite), "Agrias sprite should be deployed");
            var agriasContent = File.ReadAllText(deployedAgriasSprite);

            // With defaults now set to "original", this should pass
            Assert.Equal("original_theme", agriasContent);

            Assert.True(File.Exists(deployedGaffSprite), "Gaffgarion sprite should be deployed");
            var gaffContent = File.ReadAllText(deployedGaffSprite);
            Assert.Equal("original_theme", gaffContent); // Gaffgarion default is original
        }

        [Fact]
        public void StoryCharacterThemes_WhenAppliedAfterConfig_ShouldUseCorrectThemes()
        {
            // Arrange
            var config = new Config
            {
                Agrias = AgriasColorScheme.original,
                Gaffgarion = GaffgarionColorScheme.blacksteel_red
            };
            _configManager.SaveConfig(config);

            // Act - Initialize theme manager but DON'T apply themes yet
            var themeManager = new ThemeManager(_testSourcePath, _testModPath);
            var storyManager = themeManager.GetStoryCharacterManager();

            // Set themes from config FIRST
            storyManager.SetCurrentAgriasTheme(config.Agrias);
            storyManager.SetCurrentGaffgarionTheme(config.Gaffgarion);

            // THEN apply themes
            themeManager.ApplyInitialThemes();

            // Check what was deployed
            var deployedAgriasSprite = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_aguri_spr.bin");
            var deployedGaffSprite = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_baruna_spr.bin");

            // Assert - Now themes should match config
            Assert.True(File.Exists(deployedAgriasSprite), "Agrias sprite should be deployed");
            var agriasContent = File.ReadAllText(deployedAgriasSprite);
            Assert.Equal("original_theme", agriasContent); // Should pass when themes are set before applying

            Assert.True(File.Exists(deployedGaffSprite), "Gaffgarion sprite should be deployed");
            var gaffContent = File.ReadAllText(deployedGaffSprite);
            Assert.Equal("blacksteel_red_theme", gaffContent);
        }
    }
}