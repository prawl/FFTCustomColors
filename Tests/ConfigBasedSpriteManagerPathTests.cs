using System;
using System.IO;
using Xunit;
using FluentAssertions;
using FFTColorCustomizer.Utilities;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Services;

namespace FFTColorCustomizer.Tests
{
    public class ConfigBasedSpriteManagerPathTests : IDisposable
    {
        private readonly string _testPath;
        private readonly string _sourcePath;
        private readonly string _modPath;
        private readonly ConfigurationManager _configManager;
        private readonly CharacterDefinitionService _characterService;

        public ConfigBasedSpriteManagerPathTests()
        {
            _testPath = Path.Combine(Path.GetTempPath(), $"ConfigSpriteTest_{Guid.NewGuid()}");
            _sourcePath = Path.Combine(_testPath, "Dev", "FFTColorCustomizer"); // Dev path
            _modPath = Path.Combine(_testPath, "Reloaded", "Mods", "FFTColorCustomizer"); // Deployed path

            // Create test directories
            Directory.CreateDirectory(_sourcePath);
            Directory.CreateDirectory(_modPath);

            // Create a mock config manager
            var configPath = Path.Combine(_modPath, "Config.json");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));
            File.WriteAllText(configPath, "{}");
            _configManager = new ConfigurationManager(configPath);

            _characterService = CharacterServiceSingleton.Instance;
        }

        public void Dispose()
        {
            if (Directory.Exists(_testPath))
                Directory.Delete(_testPath, true);
        }

        [Fact]
        public void ConfigBasedSpriteManager_Should_Use_ModPath_Not_SourcePath_For_Themes()
        {
            // Arrange - create theme files ONLY in mod path (deployed location)
            var modThemeDir = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "sprites_agrias_lucavi");
            Directory.CreateDirectory(modThemeDir);

            var testSpriteFile = Path.Combine(modThemeDir, "battle_aguri_spr.bin");
            File.WriteAllText(testSpriteFile, "test sprite data");

            // Create destination directory
            var destDir = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(destDir);

            // Act
            var manager = new ConfigBasedSpriteManager(_modPath, _configManager, _characterService, _sourcePath);

            // Use reflection to call private method (or make it internal for testing)
            var method = typeof(ConfigBasedSpriteManager).GetMethod("ApplyStoryCharacterTheme",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            method.Invoke(manager, new object[] { "agrias", "aguri", "lucavi", false });

            // Assert - the theme file should be copied to the destination
            var destFile = Path.Combine(destDir, "battle_aguri_spr.bin");
            File.Exists(destFile).Should().BeTrue("theme should be applied from mod path");
            File.ReadAllText(destFile).Should().Be("test sprite data");
        }

        [Fact]
        public void ConfigBasedSpriteManager_Should_Not_Find_Themes_In_SourcePath_On_Deployed_System()
        {
            // Arrange - create theme ONLY in source path (which won't exist on deployed systems)
            var sourceThemeDir = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "sprites_mustadio_original");
            Directory.CreateDirectory(sourceThemeDir);

            var sourceSpriteFile = Path.Combine(sourceThemeDir, "battle_musu_spr.bin");
            File.WriteAllText(sourceSpriteFile, "source sprite");

            // DO NOT create the theme in mod path

            // Create destination directory
            var destDir = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(destDir);

            // Act
            var manager = new ConfigBasedSpriteManager(_modPath, _configManager, _characterService, _sourcePath);

            var method = typeof(ConfigBasedSpriteManager).GetMethod("ApplyStoryCharacterTheme",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            method.Invoke(manager, new object[] { "mustadio", "musu", "original", false });

            // Assert - no file should be copied because it only exists in source path
            var destFile = Path.Combine(destDir, "battle_musu_spr.bin");
            File.Exists(destFile).Should().BeFalse("theme should NOT be found in source path on deployed system");
        }

        [Fact]
        public void ConfigBasedSpriteManager_Should_Work_Without_SourcePath_Existing()
        {
            // Arrange - simulate deployed environment where source doesn't exist
            var nonExistentSource = Path.Combine(_testPath, "NonExistent", "Path");

            // Create theme in mod path
            var modThemeDir = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "sprites_cloud_soldier_blue");
            Directory.CreateDirectory(modThemeDir);

            var testSpriteFile = Path.Combine(modThemeDir, "battle_cloud_spr.bin");
            File.WriteAllText(testSpriteFile, "cloud sprite");

            var destDir = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

            // Act
            var manager = new ConfigBasedSpriteManager(_modPath, _configManager, _characterService, nonExistentSource);

            var method = typeof(ConfigBasedSpriteManager).GetMethod("ApplyStoryCharacterTheme",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            method.Invoke(manager, new object[] { "cloud", "cloud", "soldier_blue", false });

            // Assert
            var destFile = Path.Combine(destDir, "battle_cloud_spr.bin");
            File.Exists(destFile).Should().BeTrue("should work even when source path doesn't exist");
            File.ReadAllText(destFile).Should().Be("cloud sprite");
        }
    }
}