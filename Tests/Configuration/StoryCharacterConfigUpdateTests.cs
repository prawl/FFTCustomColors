using System;
using System.IO;
using System.Windows.Forms;
using Xunit;
using FFTColorMod.Configuration;
using FFTColorMod.Configuration.UI;
using FFTColorMod.Services;
using FFTColorMod.Tests.Helpers;
using FluentAssertions;
using FFTColorMod.Utilities;

namespace Tests.Configuration
{
    public class StoryCharacterConfigUpdateTests : IDisposable
    {
        private string _tempConfigPath;
        private string _tempModPath;

        public StoryCharacterConfigUpdateTests()
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
                        ""availableThemes"": [""original"", ""buster_sword""]
                    },
                    {
                        ""name"": ""Orlandeau"",
                        ""spriteNames"": [""oru"", ""goru"", ""voru""],
                        ""defaultTheme"": ""original"",
                        ""availableThemes"": [""original"", ""thunder_god""]
                    }
                ]
            }";
            File.WriteAllText(Path.Combine(dataPath, "StoryCharacters.json"), storyCharactersJson);

            JobClassServiceSingleton.Initialize(_tempModPath);

            // Reset the CharacterServiceSingleton to force it to reload with our test data
            CharacterServiceSingleton.Reset();
        }

        [Fact]
        public void StoryCharacter_Dropdown_Changes_Should_Update_Config_And_Apply_In_Game()
        {
            // Arrange
            var config = new Config
            {
                Agrias = "original",
                Cloud = "original",
                Orlandeau = "original"
            };

            var form = new TestConfigurationForm(config, _tempConfigPath, _tempModPath);

            // Set up the form to be ready for changes
            var initializingField = typeof(ConfigurationForm).GetField("_isInitializing",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            initializingField?.SetValue(form, false);

            var isFullyLoadedField = typeof(ConfigurationForm).GetField("_isFullyLoaded",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            isFullyLoadedField?.SetValue(form, true);

            // Get the story characters dictionary
            var storyCharsField = typeof(ConfigurationForm).GetField("_storyCharacters",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var storyCharacters = storyCharsField?.GetValue(form) as System.Collections.Generic.Dictionary<string, StoryCharacterRegistry.StoryCharacterConfig>;

            storyCharacters.Should().NotBeNull("Story characters should be loaded");
            storyCharacters.Should().ContainKey("Agrias", "Agrias should be registered as a story character");

            // Act - Simulate changing Agrias theme to ash_dark
            var agriasConfig = storyCharacters["Agrias"];
            agriasConfig.SetValue("ash_dark");

            // Also change Cloud and Orlandeau
            if (storyCharacters.ContainsKey("Cloud"))
            {
                storyCharacters["Cloud"].SetValue("buster_sword");
            }
            if (storyCharacters.ContainsKey("Orlandeau"))
            {
                storyCharacters["Orlandeau"].SetValue("thunder_god");
            }

            // Assert - The config should be updated
            config.Agrias.Should().Be("ash_dark", "Agrias theme should be updated in config");
            config.Cloud.Should().Be("buster_sword", "Cloud theme should be updated in config");
            config.Orlandeau.Should().Be("thunder_god", "Orlandeau theme should be updated in config");

            // Clean up
            form.Dispose();
        }

        [Fact]
        public void ConfigBasedSpriteManager_Should_Apply_Story_Character_Themes()
        {
            // Arrange
            var config = new Config
            {
                Agrias = "ash_dark",
                Cloud = "buster_sword",
                Orlandeau = "thunder_god"
            };

            // Debug: Check if values are set before saving
            Console.WriteLine($"Before save - config.Agrias: {config.Agrias}");
            Console.WriteLine($"Before save - config.Cloud: {config.Cloud}");
            Console.WriteLine($"Before save - config.Orlandeau: {config.Orlandeau}");

            var configManager = new ConfigurationManager(_tempConfigPath);
            configManager.SaveConfig(config); // This saves the updated config with themed values

            // Debug: Check saved JSON
            var savedJson = File.ReadAllText(_tempConfigPath);
            Console.WriteLine($"Saved JSON contains Agrias: {savedJson.Contains("Agrias")}");
            var agriasIndex = savedJson.IndexOf("Agrias");
            if (agriasIndex >= 0)
            {
                Console.WriteLine($"Agrias found at position {agriasIndex}");
                var snippet = savedJson.Substring(Math.Max(0, agriasIndex - 50), Math.Min(150, savedJson.Length - agriasIndex + 50));
                Console.WriteLine($"JSON around Agrias: {snippet}");
            }
            Console.WriteLine($"Saved JSON length: {savedJson.Length}");

            // Create mock paths for the sprite manager - matching expected structure
            var unitPath = Path.Combine(_tempModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var sourceUnitPath = Path.Combine(_tempModPath, "source", "unit", "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(unitPath);
            Directory.CreateDirectory(sourceUnitPath);

            // Create test sprite files for story characters (all sprite names)
            // Agrias has aguri and kanba sprites
            CreateTestSpriteFile(sourceUnitPath, "sprites_agrias_ash_dark", "battle_aguri_spr.bin");
            CreateTestSpriteFile(sourceUnitPath, "sprites_agrias_ash_dark", "battle_kanba_spr.bin");

            // Cloud has cloud sprite
            CreateTestSpriteFile(sourceUnitPath, "sprites_cloud_buster_sword", "battle_cloud_spr.bin");

            // Orlandeau has oru, goru, voru sprites
            CreateTestSpriteFile(sourceUnitPath, "sprites_orlandeau_thunder_god", "battle_oru_spr.bin");
            CreateTestSpriteFile(sourceUnitPath, "sprites_orlandeau_thunder_god", "battle_goru_spr.bin");
            CreateTestSpriteFile(sourceUnitPath, "sprites_orlandeau_thunder_god", "battle_voru_spr.bin");

            // Create a CharacterDefinitionService with our test data
            var dataPath = Path.Combine(_tempModPath, "Data");
            var characterService = new CharacterDefinitionService();
            characterService.LoadFromJson(Path.Combine(dataPath, "StoryCharacters.json"));

            // Pass the source folder (without the full FFTIVC path) since ConfigBasedSpriteManager adds it
            var sourceBasePath = Path.Combine(_tempModPath, "source", "unit");
            var spriteManager = new ConfigBasedSpriteManager(_tempModPath, configManager, characterService, sourceBasePath);

            // Act - Apply configuration (this should copy themed sprites)
            Console.WriteLine($"Config before apply - Agrias: {config.Agrias}, Cloud: {config.Cloud}, Orlandeau: {config.Orlandeau}");
            Console.WriteLine($"CharacterService has {characterService.GetAllCharacters().Count} characters");
            foreach (var c in characterService.GetAllCharacters())
            {
                Console.WriteLine($"  Character: {c.Name}, SpriteNames: {string.Join(", ", c.SpriteNames)}");
            }
            spriteManager.ApplyConfiguration();

            // Assert - Verify the themed sprites were copied
            File.Exists(Path.Combine(unitPath, "battle_aguri_spr.bin")).Should().BeTrue("Agrias sprite should be copied");
            File.Exists(Path.Combine(unitPath, "battle_cloud_spr.bin")).Should().BeTrue("Cloud sprite should be copied");
            File.Exists(Path.Combine(unitPath, "battle_oru_spr.bin")).Should().BeTrue("Orlandeau sprite should be copied");

            // Verify the content indicates the correct theme
            var agriasContent = File.ReadAllText(Path.Combine(unitPath, "battle_aguri_spr.bin"));
            agriasContent.Should().Contain("ash_dark", "Agrias should use ash_dark theme");

            var cloudContent = File.ReadAllText(Path.Combine(unitPath, "battle_cloud_spr.bin"));
            cloudContent.Should().Contain("buster_sword", "Cloud should use buster_sword theme");

            var orlandeauContent = File.ReadAllText(Path.Combine(unitPath, "battle_oru_spr.bin"));
            orlandeauContent.Should().Contain("thunder_god", "Orlandeau should use thunder_god theme");
        }

        private void CreateTestSpriteFile(string sourceUnitPath, string themeDir, string fileName)
        {
            var themePath = Path.Combine(sourceUnitPath, themeDir);
            Directory.CreateDirectory(themePath);
            var filePath = Path.Combine(themePath, fileName);
            // Write content that includes the theme name so we can verify correct file was copied
            File.WriteAllText(filePath, $"Test sprite for {themeDir}");
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