using System;
using System.IO;
using Xunit;
using FluentAssertions;
using FFTColorCustomizer.Utilities;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Services;
using Tests.Helpers;
using FFTColorCustomizer.Core;

namespace Tests.Utilities
{
    public class ConfigBasedSpriteManagerJobSpecificTests : IDisposable
    {
        private readonly string _testModPath;
        private readonly string _testSourcePath;
        private readonly ConfigurationManager _configManager;
        private readonly ConfigBasedSpriteManager _spriteManager;
        private readonly CharacterDefinitionService _characterService;
        private readonly JobClassDefinitionService _jobClassService;
        private readonly string _unitPath;
        private readonly string _sourceUnitPath;

        public ConfigBasedSpriteManagerJobSpecificTests()
        {
            _testModPath = Path.Combine(Path.GetTempPath(), "TestMod_" + Guid.NewGuid().ToString());
            _testSourcePath = Path.Combine(Path.GetTempPath(), "TestSource_" + Guid.NewGuid().ToString());
            _unitPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            _sourceUnitPath = Path.Combine(_testSourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

            Directory.CreateDirectory(_unitPath);
            Directory.CreateDirectory(_sourceUnitPath);

            // Create a mock JobClassDefinitionService and CharacterDefinitionService
            var dataPath = Path.Combine(_testModPath, "Data");
            Directory.CreateDirectory(dataPath);
            CreateMockJobClassesJson(Path.Combine(dataPath, "JobClasses.json"));
            CreateMockStoryCharactersJson(Path.Combine(dataPath, "StoryCharacters.json"));

            _jobClassService = new JobClassDefinitionService(dataPath);
            _characterService = new CharacterDefinitionService();
            _characterService.LoadFromJson(Path.Combine(dataPath, "StoryCharacters.json"));

            var configPath = Path.Combine(_testModPath, "Config.json");
            _configManager = new ConfigurationManager(configPath, _jobClassService);

            _spriteManager = new ConfigBasedSpriteManager(_testModPath, _configManager, _characterService, _testSourcePath);
        }

        private void CreateMockJobClassesJson(string path)
        {
            var json = @"{
  ""sharedThemes"": [
    ""original"",
    ""corpse_brigade"",
    ""lucavi""
  ],
  ""jobClasses"": [
    {
      ""name"": ""Mediator_Male"",
      ""displayName"": ""Mediator (Male)"",
      ""spriteName"": ""battle_waju_m_spr.bin"",
      ""defaultTheme"": ""original"",
      ""gender"": ""Male"",
      ""jobType"": ""Mediator"",
      ""jobSpecificThemes"": [
        ""young_maiden"",
        ""holy_knight""
      ]
    },
    {
      ""name"": ""Mediator_Female"",
      ""displayName"": ""Mediator (Female)"",
      ""spriteName"": ""battle_waju_w_spr.bin"",
      ""defaultTheme"": ""original"",
      ""gender"": ""Female"",
      ""jobType"": ""Mediator"",
      ""jobSpecificThemes"": [
        ""young_maiden"",
        ""holy_knight""
      ]
    }
  ]
}";
            File.WriteAllText(path, json);
        }

        private void CreateMockStoryCharactersJson(string path)
        {
            var json = @"{
  ""characters"": [
    {
      ""name"": ""Agrias"",
      ""displayName"": ""Agrias"",
      ""spriteNames"": [""agria1""],
      ""availableThemes"": [""original""]
    }
  ]
}";
            File.WriteAllText(path, json);
        }

        [Fact]
        public void ApplyConfiguration_WithJobSpecificTheme_ShouldCopyFromJobSpecificDirectory()
        {
            // Arrange
            var config = new Config();
            config["Mediator_Male"] = "holy_knight"; // This is a job-specific theme for Mediator
            _configManager.SaveConfig(config);

            // Create the job-specific theme directory and file
            // CRITICAL FIX: Use mod path (_unitPath) instead of source path
            var jobSpecificDir = Path.Combine(_unitPath, "sprites_mediator_holy_knight");
            Directory.CreateDirectory(jobSpecificDir);
            var jobSpecificSprite = Path.Combine(jobSpecificDir, "battle_waju_m_spr.bin");
            File.WriteAllText(jobSpecificSprite, "holy_knight_mediator_content");

            // Also create generic theme directory (which should NOT be used)
            var genericDir = Path.Combine(_unitPath, "sprites_holy_knight");
            Directory.CreateDirectory(genericDir);
            var genericSprite = Path.Combine(genericDir, "battle_waju_m_spr.bin");
            File.WriteAllText(genericSprite, "generic_holy_knight_content");

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert
            var destFile = Path.Combine(_unitPath, "battle_waju_m_spr.bin");
            File.Exists(destFile).Should().BeTrue("sprite file should be copied to destination");
            File.ReadAllText(destFile).Should().Be("holy_knight_mediator_content",
                "should use job-specific theme, not generic theme");
        }

        [Fact]
        public void ApplyConfiguration_WithSharedTheme_ShouldCopyFromGenericDirectory()
        {
            // Arrange
            var config = new Config();
            config["Mediator_Male"] = "corpse_brigade"; // This is a shared theme (not job-specific)
            _configManager.SaveConfig(config);

            // Create only the generic theme directory
            // CRITICAL FIX: Use mod path (_unitPath) instead of source path
            var genericDir = Path.Combine(_unitPath, "sprites_corpse_brigade");
            Directory.CreateDirectory(genericDir);
            var genericSprite = Path.Combine(genericDir, "battle_waju_m_spr.bin");
            File.WriteAllText(genericSprite, "corpse_brigade_content");

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert
            var destFile = Path.Combine(_unitPath, "battle_waju_m_spr.bin");
            File.Exists(destFile).Should().BeTrue("sprite file should be copied to destination");
            File.ReadAllText(destFile).Should().Be("corpse_brigade_content",
                "should use generic theme directory for shared themes");
        }

        [Fact]
        public void ApplyConfiguration_WithJobSpecificTheme_Female_ShouldCopyFromJobSpecificDirectory()
        {
            // Arrange
            var config = new Config();
            config["Mediator_Female"] = "young_maiden"; // Job-specific theme for female Mediator
            _configManager.SaveConfig(config);

            // CRITICAL FIX: After the path fix, themes should be in mod path (_unitPath), not source path
            var jobSpecificDir = Path.Combine(_unitPath, "sprites_mediator_young_maiden");
            Directory.CreateDirectory(jobSpecificDir);
            var jobSpecificSprite = Path.Combine(jobSpecificDir, "battle_waju_w_spr.bin");
            File.WriteAllText(jobSpecificSprite, "young_maiden_mediator_female_content");

            // Act
            _spriteManager.ApplyConfiguration();

            // Assert
            var destFile = Path.Combine(_unitPath, "battle_waju_w_spr.bin");
            File.Exists(destFile).Should().BeTrue("female sprite file should be copied to destination");
            File.ReadAllText(destFile).Should().Be("young_maiden_mediator_female_content",
                "should use job-specific theme for female variant");
        }

        public void Dispose()
        {
            if (Directory.Exists(_testModPath))
                Directory.Delete(_testModPath, true);
            if (Directory.Exists(_testSourcePath))
                Directory.Delete(_testSourcePath, true);
        }
    }
}