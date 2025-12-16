using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FFTColorMod.Configuration;
using FFTColorMod.Configuration.UI;
using FFTColorMod.Services;

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
                EnumType = "AgriasColorScheme"
            });
            service.AddCharacter(new CharacterDefinition
            {
                Name = "Cloud",
                SpriteNames = new[] { "cloud" },
                DefaultTheme = "original",
                AvailableThemes = new[] { "original", "dark" },
                EnumType = "CloudColorScheme"
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
            Assert.Equal(typeof(AgriasColorScheme), agriasConfig.EnumType);
        }

        [Fact]
        public void GetStoryCharactersFromService_Should_Auto_Discover_From_Attributes()
        {
            // Arrange
            var service = new CharacterDefinitionService();
            var config = new Config();

            // Act
            StoryCharacterRegistry.AutoDiscoverCharacters(service);
            var characters = StoryCharacterRegistry.GetStoryCharactersFromService(config, service);

            // Assert
            // Should find at least Agrias, Cloud, and other characters with StoryCharacter attributes
            Assert.True(characters.Count > 0, "Should auto-discover characters from attributes");
            Assert.True(characters.ContainsKey("Agrias"), "Should find Agrias from attribute");
        }
    }
}