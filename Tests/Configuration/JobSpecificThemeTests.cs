using Xunit;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Utilities;
using FFTColorCustomizer.Services;
using FluentAssertions;
using System.IO;
using Tests.Helpers;

namespace Tests.Configuration
{
    public class JobSpecificThemeTests : IDisposable
    {
        private readonly TestFileSystem _fileSystem;
        private readonly string _testPath;
        private readonly ConfigBasedSpriteManager _spriteManager;
        private readonly ConfigurationManager _configManager;

        public JobSpecificThemeTests()
        {
            _testPath = Path.Combine(Path.GetTempPath(), "FFTColorTest_" + Path.GetRandomFileName());
            _fileSystem = new TestFileSystem(_testPath);

            // Create directory structure
            var unitPath = Path.Combine(_testPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(unitPath);

            // CRITICAL FIX: After the path fix, sprites should be in mod path (_testPath), not source path
            // ConfigBasedSpriteManager now looks in _modPath for theme files
            var modSpritePath = Path.Combine(_testPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(modSpritePath);

            // Create shared theme folders in mod path
            Directory.CreateDirectory(Path.Combine(modSpritePath, "sprites_emerald_dragon"));
            Directory.CreateDirectory(Path.Combine(modSpritePath, "sprites_original"));

            // Create job-specific theme folders for Mediator in mod path
            Directory.CreateDirectory(Path.Combine(modSpritePath, "sprites_mediator_holy_knight"));
            Directory.CreateDirectory(Path.Combine(modSpritePath, "sprites_mediator_wind_dancer"));

            // Create dummy sprite files in mod path
            CreateDummySprite(Path.Combine(modSpritePath, "sprites_original", "battle_waju_m_spr.bin"), "original");
            CreateDummySprite(Path.Combine(modSpritePath, "sprites_emerald_dragon", "battle_waju_m_spr.bin"), "emerald_dragon");
            CreateDummySprite(Path.Combine(modSpritePath, "sprites_mediator_holy_knight", "battle_waju_m_spr.bin"), "mediator_holy_knight");
            CreateDummySprite(Path.Combine(modSpritePath, "sprites_mediator_wind_dancer", "battle_waju_m_spr.bin"), "mediator_wind_dancer");

            // Create JobClassDefinitionService for ConfigurationManager
            var dataPath = Path.Combine(_testPath, "Data");
            Directory.CreateDirectory(dataPath);

            // Create a minimal JobClasses.json for the test
            var jobClassesJson = @"{
  ""sharedThemes"": [""emerald_dragon""],
  ""jobClasses"": [{
    ""name"": ""Mediator_Male"",
    ""displayName"": ""Mediator (Male)"",
    ""spriteName"": ""battle_waju_m_spr.bin"",
    ""defaultTheme"": ""original"",
    ""gender"": ""Male"",
    ""jobType"": ""Mediator"",
    ""jobSpecificThemes"": [""holy_knight"", ""wind_dancer""]
  }]
}";
            File.WriteAllText(Path.Combine(dataPath, "JobClasses.json"), jobClassesJson);
            var jobClassService = new JobClassDefinitionService(dataPath);

            _configManager = new ConfigurationManager(Path.Combine(_testPath, "Config.json"), jobClassService);

            // Pass the full source path (parent of FFTIVC)
            var sourceBasePath = Path.Combine(_testPath, "Source");
            // Use backward compatibility constructor (uses CharacterServiceSingleton)
            _spriteManager = new ConfigBasedSpriteManager(_testPath, _configManager, sourceBasePath);
        }

        private void CreateDummySprite(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, System.Text.Encoding.UTF8.GetBytes(content));
        }

        [Fact]
        public void Should_Apply_Shared_Theme_To_Mediator()
        {
            // Arrange
            var config = new Config();
            config["Mediator_Male"] = "emerald_dragon";
            _configManager.SaveConfig(config);

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert
            var appliedSprite = Path.Combine(_testPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_waju_m_spr.bin");
            File.Exists(appliedSprite).Should().BeTrue("sprite should be copied to deployment folder");

            var content = File.ReadAllText(appliedSprite);
            content.Should().Be("emerald_dragon", "emerald_dragon theme should be applied");
        }

        [Fact]
        public void Should_Apply_JobSpecific_Theme_To_Mediator()
        {
            // Arrange
            var config = new Config();
            config["Mediator_Male"] = "holy_knight";  // This is a job-specific theme
            _configManager.SaveConfig(config);

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert
            var appliedSprite = Path.Combine(_testPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_waju_m_spr.bin");
            File.Exists(appliedSprite).Should().BeTrue("sprite should be copied to deployment folder");

            var content = File.ReadAllText(appliedSprite);
            content.Should().Be("mediator_holy_knight", "job-specific holy_knight theme should be applied from sprites_mediator_holy_knight folder");
        }

        [Fact]
        public void Should_Apply_JobSpecific_WindDancer_Theme_To_Mediator()
        {
            // Arrange
            var config = new Config();
            config["Mediator_Male"] = "wind_dancer";  // Another job-specific theme
            _configManager.SaveConfig(config);

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert
            var appliedSprite = Path.Combine(_testPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_waju_m_spr.bin");
            File.Exists(appliedSprite).Should().BeTrue("sprite should be copied to deployment folder");

            var content = File.ReadAllText(appliedSprite);
            content.Should().Be("mediator_wind_dancer", "job-specific wind_dancer theme should be applied from sprites_mediator_wind_dancer folder");
        }

        [Fact]
        public void Should_Check_JobSpecific_Folder_Before_Shared_Folder()
        {
            // This test ensures the system checks job-specific folders first
            // Create both a shared "holy_knight" and job-specific "mediator_holy_knight"

            // Arrange
            var sourcePath = Path.Combine(_testPath, "Source", "FFTIVC", "data", "enhanced", "fftpack", "unit");

            // Create a shared holy_knight folder (this should NOT be used for mediator)
            Directory.CreateDirectory(Path.Combine(sourcePath, "sprites_holy_knight"));
            CreateDummySprite(Path.Combine(sourcePath, "sprites_holy_knight", "battle_waju_m_spr.bin"), "shared_holy_knight");

            var config = new Config();
            config["Mediator_Male"] = "holy_knight";
            _configManager.SaveConfig(config);

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert
            var appliedSprite = Path.Combine(_testPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_waju_m_spr.bin");
            var content = File.ReadAllText(appliedSprite);
            content.Should().Be("mediator_holy_knight",
                "should use job-specific sprites_mediator_holy_knight, not shared sprites_holy_knight");
        }

        [Fact]
        public void Should_Fall_Back_To_Shared_Theme_If_JobSpecific_Not_Found()
        {
            // Arrange
            var config = new Config();
            config["Mediator_Male"] = "emerald_dragon";  // This is a shared theme, no job-specific version
            _configManager.SaveConfig(config);

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert
            var appliedSprite = Path.Combine(_testPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_waju_m_spr.bin");
            var content = File.ReadAllText(appliedSprite);
            content.Should().Be("emerald_dragon",
                "should use shared sprites_emerald_dragon when no job-specific folder exists");
        }

        public void Dispose()
        {
            _fileSystem?.Dispose();

            // Clean up test directory
            if (Directory.Exists(_testPath))
            {
                try
                {
                    Directory.Delete(_testPath, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}