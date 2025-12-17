using System;
using System.IO;
using Xunit;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.Utilities;
using FluentAssertions;

namespace Tests.Configuration
{
    public class StoryCharacterOriginalThemeTests : IDisposable
    {
        private string _tempConfigPath;
        private string _tempModPath;

        public StoryCharacterOriginalThemeTests()
        {
            _tempConfigPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "Config.json");
            _tempModPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "Mod");
            Directory.CreateDirectory(Path.GetDirectoryName(_tempConfigPath));
            Directory.CreateDirectory(_tempModPath);

            // Initialize services
            var dataPath = Path.Combine(_tempModPath, "Data");
            Directory.CreateDirectory(dataPath);

            // Create a test StoryCharacters.json file
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
                        ""availableThemes"": [""original"", ""black_gold""]
                    },
                    {
                        ""name"": ""Reis"",
                        ""spriteNames"": [""reze"", ""reze_d""],
                        ""defaultTheme"": ""original"",
                        ""availableThemes"": [""original"", ""rose_pink""]
                    }
                ]
            }";
            File.WriteAllText(Path.Combine(dataPath, "StoryCharacters.json"), storyCharactersJson);

            JobClassServiceSingleton.Initialize(_tempModPath);
            CharacterServiceSingleton.Reset();
        }

        [Fact]
        public void StoryCharacter_Should_Apply_Original_Theme_When_Set_To_Original()
        {
            // Arrange
            var config = new Config
            {
                Agrias = "original",
                Cloud = "original",
                Reis = "original"
            };

            var configManager = new ConfigurationManager(_tempConfigPath);
            configManager.SaveConfig(config);

            // Create mock paths for the sprite manager - matching expected structure
            var unitPath = Path.Combine(_tempModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var sourceUnitPath = Path.Combine(_tempModPath, "source", "unit", "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(unitPath);
            Directory.CreateDirectory(sourceUnitPath);

            // Create original sprite files for story characters
            CreateTestSpriteFile(sourceUnitPath, "sprites_agrias_original", "battle_aguri_spr.bin", "agrias original");
            CreateTestSpriteFile(sourceUnitPath, "sprites_agrias_original", "battle_kanba_spr.bin", "agrias original");
            CreateTestSpriteFile(sourceUnitPath, "sprites_cloud_original", "battle_cloud_spr.bin", "cloud original");
            CreateTestSpriteFile(sourceUnitPath, "sprites_reis_original", "battle_reze_spr.bin", "reis original");
            CreateTestSpriteFile(sourceUnitPath, "sprites_reis_original", "battle_reze_d_spr.bin", "reis original");

            // Create themed sprite files as well
            CreateTestSpriteFile(sourceUnitPath, "sprites_agrias_ash_dark", "battle_aguri_spr.bin", "agrias ash_dark");
            CreateTestSpriteFile(sourceUnitPath, "sprites_cloud_black_gold", "battle_cloud_spr.bin", "cloud black_gold");
            CreateTestSpriteFile(sourceUnitPath, "sprites_reis_rose_pink", "battle_reze_spr.bin", "reis rose_pink");

            // Create a CharacterDefinitionService with our test data
            var dataPath = Path.Combine(_tempModPath, "Data");
            var characterService = new CharacterDefinitionService();
            characterService.LoadFromJson(Path.Combine(dataPath, "StoryCharacters.json"));

            // Pass the source folder (without the full FFTIVC path) since ConfigBasedSpriteManager adds it
            var sourceBasePath = Path.Combine(_tempModPath, "source", "unit");
            var spriteManager = new ConfigBasedSpriteManager(_tempModPath, configManager, characterService, sourceBasePath);

            // Act - Apply configuration (this should copy original sprites)
            spriteManager.ApplyConfiguration();

            // Assert - Verify the original sprites were copied
            File.Exists(Path.Combine(unitPath, "battle_aguri_spr.bin")).Should().BeTrue("Agrias aguri sprite should be copied");
            File.Exists(Path.Combine(unitPath, "battle_kanba_spr.bin")).Should().BeTrue("Agrias kanba sprite should be copied");
            File.Exists(Path.Combine(unitPath, "battle_cloud_spr.bin")).Should().BeTrue("Cloud sprite should be copied");
            File.Exists(Path.Combine(unitPath, "battle_reze_spr.bin")).Should().BeTrue("Reis reze sprite should be copied");
            File.Exists(Path.Combine(unitPath, "battle_reze_d_spr.bin")).Should().BeTrue("Reis reze_d sprite should be copied");

            // Verify the content indicates the correct original theme
            var agriasContent = File.ReadAllText(Path.Combine(unitPath, "battle_aguri_spr.bin"));
            agriasContent.Should().Contain("agrias original", "Agrias should use original theme");

            var cloudContent = File.ReadAllText(Path.Combine(unitPath, "battle_cloud_spr.bin"));
            cloudContent.Should().Contain("cloud original", "Cloud should use original theme");

            var reisContent = File.ReadAllText(Path.Combine(unitPath, "battle_reze_spr.bin"));
            reisContent.Should().Contain("reis original", "Reis should use original theme");
        }

        [Fact]
        public void StoryCharacter_Should_Switch_Between_Themed_And_Original()
        {
            // Arrange - Start with themed
            var config = new Config
            {
                Agrias = "ash_dark",
                Cloud = "black_gold",
                Reis = "rose_pink"
            };

            var configManager = new ConfigurationManager(_tempConfigPath);
            configManager.SaveConfig(config);

            // Create mock paths for the sprite manager
            var unitPath = Path.Combine(_tempModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var sourceUnitPath = Path.Combine(_tempModPath, "source", "unit", "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(unitPath);
            Directory.CreateDirectory(sourceUnitPath);

            // Create sprite files
            CreateTestSpriteFile(sourceUnitPath, "sprites_agrias_original", "battle_aguri_spr.bin", "agrias original");
            CreateTestSpriteFile(sourceUnitPath, "sprites_agrias_ash_dark", "battle_aguri_spr.bin", "agrias ash_dark");
            CreateTestSpriteFile(sourceUnitPath, "sprites_cloud_original", "battle_cloud_spr.bin", "cloud original");
            CreateTestSpriteFile(sourceUnitPath, "sprites_cloud_black_gold", "battle_cloud_spr.bin", "cloud black_gold");
            CreateTestSpriteFile(sourceUnitPath, "sprites_reis_original", "battle_reze_spr.bin", "reis original");
            CreateTestSpriteFile(sourceUnitPath, "sprites_reis_rose_pink", "battle_reze_spr.bin", "reis rose_pink");

            var dataPath = Path.Combine(_tempModPath, "Data");
            var characterService = new CharacterDefinitionService();
            characterService.LoadFromJson(Path.Combine(dataPath, "StoryCharacters.json"));

            var sourceBasePath = Path.Combine(_tempModPath, "source", "unit");
            var spriteManager = new ConfigBasedSpriteManager(_tempModPath, configManager, characterService, sourceBasePath);

            // Act 1 - Apply themed configuration
            spriteManager.ApplyConfiguration();

            // Assert 1 - Verify themed sprites
            var agriasContent = File.ReadAllText(Path.Combine(unitPath, "battle_aguri_spr.bin"));
            agriasContent.Should().Contain("agrias ash_dark", "Agrias should use ash_dark theme initially");

            // Act 2 - Change to original
            config.Agrias = "original";
            config.Cloud = "original";
            config.Reis = "original";
            configManager.SaveConfig(config);
            spriteManager.UpdateConfiguration(config);

            // Assert 2 - Verify original sprites are applied
            agriasContent = File.ReadAllText(Path.Combine(unitPath, "battle_aguri_spr.bin"));
            agriasContent.Should().Contain("agrias original", "Agrias should revert to original theme");

            var cloudContent = File.ReadAllText(Path.Combine(unitPath, "battle_cloud_spr.bin"));
            cloudContent.Should().Contain("cloud original", "Cloud should revert to original theme");

            var reisContent = File.ReadAllText(Path.Combine(unitPath, "battle_reze_spr.bin"));
            reisContent.Should().Contain("reis original", "Reis should revert to original theme");
        }

        private void CreateTestSpriteFile(string sourceUnitPath, string themeDir, string fileName, string content)
        {
            var themePath = Path.Combine(sourceUnitPath, themeDir);
            Directory.CreateDirectory(themePath);
            var filePath = Path.Combine(themePath, fileName);
            // Write content that includes the theme name so we can verify correct file was copied
            File.WriteAllText(filePath, $"Test sprite: {content}");
        }

        public void Dispose()
        {
            if (Directory.Exists(Path.GetDirectoryName(_tempConfigPath)))
            {
                Directory.Delete(Path.GetDirectoryName(_tempConfigPath), true);
            }
            if (Directory.Exists(_tempModPath))
            {
                Directory.Delete(_tempModPath, true);
            }
        }
    }
}
