using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using FFTColorMod.Services;

namespace Tests.Services
{
    public class CharacterDefinitionServiceTests
    {
        [Fact]
        public void CharacterDefinitionService_Should_Initialize_Empty()
        {
            // Arrange & Act
            var service = new CharacterDefinitionService();

            // Assert
            Assert.NotNull(service);
            Assert.NotNull(service.GetAllCharacters());
            Assert.Empty(service.GetAllCharacters());
        }

        [Fact]
        public void AddCharacter_Should_Add_Character_Definition()
        {
            // Arrange
            var service = new CharacterDefinitionService();
            var character = new CharacterDefinition
            {
                Name = "Agrias",
                SpriteNames = new[] { "aguri", "kanba" },
                DefaultTheme = "original",
                AvailableThemes = new[] { "original", "ash_dark", "blackguard_gold" }
            };

            // Act
            service.AddCharacter(character);

            // Assert
            var characters = service.GetAllCharacters();
            Assert.Single(characters);
            Assert.Equal("Agrias", characters.First().Name);
        }

        [Fact]
        public void GetCharacterByName_Should_Return_Correct_Character()
        {
            // Arrange
            var service = new CharacterDefinitionService();
            var agrias = new CharacterDefinition
            {
                Name = "Agrias",
                SpriteNames = new[] { "aguri", "kanba" },
                DefaultTheme = "original",
                AvailableThemes = new[] { "original", "ash_dark", "blackguard_gold" }
            };
            var cloud = new CharacterDefinition
            {
                Name = "Cloud",
                SpriteNames = new[] { "cloud" },
                DefaultTheme = "original",
                AvailableThemes = new[] { "original", "dark", "light" }
            };

            service.AddCharacter(agrias);
            service.AddCharacter(cloud);

            // Act
            var result = service.GetCharacterByName("Agrias");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Agrias", result.Name);
            Assert.Equal(2, result.SpriteNames.Length);
            Assert.Contains("aguri", result.SpriteNames);
        }

        [Fact]
        public void LoadFromJson_Should_Load_Character_Definitions()
        {
            // Arrange
            var service = new CharacterDefinitionService();
            var jsonContent = @"{
                ""characters"": [
                    {
                        ""name"": ""Agrias"",
                        ""spriteNames"": [""aguri"", ""kanba""],
                        ""defaultTheme"": ""original"",
                        ""availableThemes"": [""original"", ""ash_dark"", ""blackguard_gold""]
                    },
                    {
                        ""name"": ""Cloud"",
                        ""spriteNames"": [""cloud""],
                        ""defaultTheme"": ""original"",
                        ""availableThemes"": [""original"", ""dark"", ""light""]
                    }
                ]
            }";

            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, jsonContent);

                // Act
                service.LoadFromJson(tempFile);

                // Assert
                var characters = service.GetAllCharacters();
                Assert.Equal(2, characters.Count);

                var agrias = service.GetCharacterByName("Agrias");
                Assert.NotNull(agrias);
                Assert.Equal("Agrias", agrias.Name);
                Assert.Contains("aguri", agrias.SpriteNames);

                var cloud = service.GetCharacterByName("Cloud");
                Assert.NotNull(cloud);
                Assert.Equal("Cloud", cloud.Name);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public void LoadFromJson_Should_Load_StoryCharacters_File()
        {
            // Arrange
            var service = new CharacterDefinitionService();

            // Try to find the file in the project directory
            var baseDir = Directory.GetCurrentDirectory();
            var storyCharactersPath = Path.Combine(baseDir, "ColorMod", "Data", "StoryCharacters.json");

            // If not found, try the relative path from test output directory
            if (!File.Exists(storyCharactersPath))
            {
                storyCharactersPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..", "..", "..", "..",
                    "ColorMod", "Data", "StoryCharacters.json"
                );
            }

            // Skip test if file doesn't exist (not a failure, just not available in test environment)
            if (!File.Exists(storyCharactersPath))
            {
                // For CI/CD or when file doesn't exist, we can skip this test
                return;
            }

            // Act
            service.LoadFromJson(storyCharactersPath);

            // Assert
            var characters = service.GetAllCharacters();
            Assert.True(characters.Count >= 10, $"Expected at least 10 characters, found {characters.Count}");

            var agrias = service.GetCharacterByName("Agrias");
            Assert.NotNull(agrias);
            Assert.Equal("Agrias", agrias.Name);
            Assert.Contains("aguri", agrias.SpriteNames);
            Assert.Contains("kanba", agrias.SpriteNames);
            Assert.Equal("AgriasColorScheme", agrias.EnumType);

            var cloud = service.GetCharacterByName("Cloud");
            Assert.NotNull(cloud);
            Assert.Equal("CloudColorScheme", cloud.EnumType);
        }
    }
}