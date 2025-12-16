using System;
using System.Linq;
using Xunit;
using FFTColorMod.Configuration;
using FFTColorMod.Configuration.UI;
using FFTColorMod.Services;

namespace Tests.Configuration.UI
{
    public class StoryCharacterRegistryMigrationTests
    {
        [Fact]
        public void Registry_Should_Use_CharacterService_Correctly()
        {
            // Arrange
            var config = new Config();

            // Test that new registry uses service correctly
            var service = new CharacterDefinitionService();
            StoryCharacterRegistry.AutoDiscoverCharacters(service);
            var characters = StoryCharacterRegistry.GetStoryCharactersFromService(config, service);

            // Assert - Should have discovered characters
            Assert.True(characters.Count > 0, "Should discover characters from attributes");

            // Test backward compatibility method (uses singleton)
            var charactersFromSingleton = StoryCharacterRegistry.GetStoryCharacters(config);
            Assert.True(charactersFromSingleton.Count > 0, "Backward compatibility method should work");
        }

        [Fact]
        public void Registry_Should_Support_All_Operations()
        {
            // Arrange
            var config = new Config();
            var service = new CharacterDefinitionService();
            StoryCharacterRegistry.AutoDiscoverCharacters(service);
            var characters = StoryCharacterRegistry.GetStoryCharactersFromService(config, service);

            // Act & Assert - Test setting and getting values
            if (characters.ContainsKey("Agrias"))
            {
                var agriasConfig = characters["Agrias"];

                // Set a value
                agriasConfig.SetValue(AgriasColorScheme.ash_dark);

                // Get the value back
                var value = agriasConfig.GetValue();
                Assert.Equal(AgriasColorScheme.ash_dark, value);

                // Verify it was set in the config
                Assert.Equal(AgriasColorScheme.ash_dark, config.Agrias);
            }
        }

        [Fact]
        public void Should_Load_Characters_From_Json_File()
        {
            // Arrange
            var config = new Config();
            var service = new CharacterDefinitionService();

            // Load from the actual StoryCharacters.json file if it exists
            var jsonPath = System.IO.Path.Combine(
                System.IO.Directory.GetCurrentDirectory(),
                "ColorMod", "Data", "StoryCharacters.json"
            );

            if (!System.IO.File.Exists(jsonPath))
            {
                // Try alternative path
                jsonPath = System.IO.Path.Combine(
                    System.IO.Directory.GetCurrentDirectory(),
                    "..", "..", "..", "..",
                    "ColorMod", "Data", "StoryCharacters.json"
                );
            }

            if (System.IO.File.Exists(jsonPath))
            {
                // Act
                service.LoadFromJson(jsonPath);
                var characters = StoryCharacterRegistry.GetStoryCharactersFromService(config, service);

                // Assert
                Assert.True(characters.Count > 0, "Should load characters from JSON");
                Assert.True(characters.ContainsKey("Agrias"), "Should have Agrias");
                Assert.True(characters.ContainsKey("Cloud"), "Should have Cloud");
                Assert.True(characters.ContainsKey("Orlandeau"), "Should have Orlandeau");
            }
        }
    }
}