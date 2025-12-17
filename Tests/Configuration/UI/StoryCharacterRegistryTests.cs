using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Services;

namespace Tests.Configuration.UI
{
    public class StoryCharacterRegistryTests
    {
        [Fact]
        public void GetStoryCharactersFromService_Should_Return_All_Characters()
        {
            // Arrange
            var service = new CharacterDefinitionService();
            service.AddCharacter(new CharacterDefinition
            {
                Name = "Agrias",
                SpriteNames = new[] { "aguri", "kanba" },
                DefaultTheme = "original",
                AvailableThemes = new[] { "original", "ash_dark" },
                EnumType = "string"
            });
            service.AddCharacter(new CharacterDefinition
            {
                Name = "Cloud",
                SpriteNames = new[] { "cloud" },
                DefaultTheme = "original",
                AvailableThemes = new[] { "original" },
                EnumType = "string"
            });

            var config = new Config();

            // Act
            var characters = StoryCharacterRegistry.GetStoryCharactersFromService(config, service);

            // Assert
            Assert.Equal(2, characters.Count);
            Assert.True(characters.ContainsKey("Agrias"));
            Assert.True(characters.ContainsKey("Cloud"));

            var agriasConfig = characters["Agrias"];
            Assert.Equal("Agrias", agriasConfig.Name);
            Assert.Equal(typeof(string), agriasConfig.EnumType);
        }

        [Fact]
        public void GetStoryCharactersFromService_Should_Load_From_JSON_File()
        {
            // Arrange
            var service = new CharacterDefinitionService();
            var config = new Config();

            // Load characters directly from JSON to test the new system
            var jsonPath = @"C:\Users\ptyRa\Dev\FFTColorCustomizer\ColorMod\Data\StoryCharacters.json";
            if (System.IO.File.Exists(jsonPath))
            {
                service.LoadFromJson(jsonPath);
            }

            // Debug: Check how many characters are loaded
            var allCharacters = service.GetAllCharacters();
            Assert.True(allCharacters.Count > 0, $"Service should have loaded characters from JSON, but got {allCharacters.Count}");

            // Act
            var characters = StoryCharacterRegistry.GetStoryCharactersFromService(config, service);

            // Debug: Output what we got
            var characterNames = string.Join(", ", characters.Keys);

            // Assert
            // Should load characters from StoryCharacters.json - expect 9 characters total
            // (Agrias, Cloud, Mustadio, Orlandeau, Reis, Rapha, Marach, Beowulf, Meliadoul)
            Assert.True(characters.Count >= 9, $"Should load at least 9 characters from JSON, but got {characters.Count}. Characters: {characterNames}");
            Assert.True(characters.ContainsKey("Agrias"), "Should find Agrias from JSON");
            Assert.True(characters.ContainsKey("Cloud"), "Should find Cloud from JSON");
            Assert.True(characters.ContainsKey("Orlandeau"), "Should find Orlandeau from JSON");

            // Verify Alma and Delita are NOT present (they were removed)
            Assert.False(characters.ContainsKey("Alma"), "Alma should not be present (removed during refactoring)");
            Assert.False(characters.ContainsKey("Delita"), "Delita should not be present (removed during refactoring)");
        }
    }
}
