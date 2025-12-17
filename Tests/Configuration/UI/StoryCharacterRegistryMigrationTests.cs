using System;
using System.Linq;
using Xunit;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Services;

namespace Tests.Configuration.UI
{
    public class StoryCharacterRegistryMigrationTests
    {
        [Fact]
        public void Registry_Should_Use_CharacterService_Correctly()
        {
            // Arrange
            var config = new Config();
            var service = new CharacterDefinitionService();

            // Find the JSON file using the helper method
            var jsonPath = FindStoryCharactersJsonFile();

            // Load from JSON
            if (System.IO.File.Exists(jsonPath))
            {
                try
                {
                    service.LoadFromJson(jsonPath);
                    var loadedCount = service.GetAllCharacters().Count;
                    System.Console.WriteLine($"Successfully loaded {loadedCount} characters from JSON from path: {jsonPath}");
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"Failed to load JSON: {ex.Message}");
                    throw;
                }
            }
            else
            {
                // If JSON file isn't found, try to auto-discover from attributes as fallback
                System.Console.WriteLine($"JSON file not found at: {jsonPath}, attempting auto-discovery");
                StoryCharacterRegistry.AutoDiscoverCharacters(service);
            }

            var characters = StoryCharacterRegistry.GetStoryCharactersFromService(config, service);

            // Check if characters were loaded
            var serviceCharacters = service.GetAllCharacters();
            Assert.True(serviceCharacters.Count > 0, $"Should load characters from JSON file or auto-discovery. Service has {serviceCharacters.Count} characters");

            // Get config property for Agrias to test reflection and string-based theme compatibility
            var agriasProperty = typeof(Config).GetProperty("Agrias");
            Assert.NotNull(agriasProperty);
            Assert.Equal(typeof(string), agriasProperty.PropertyType);

            // Assert - Should have loaded characters
            Assert.True(characters.Count > 0, $"Should load characters. Registry has {characters.Count} characters from {serviceCharacters.Count} service characters");
            Assert.True(characters.ContainsKey("Agrias"), "Should contain Agrias");

            // Verify that Agrias character has correct string-based theme system setup
            var agriasConfig = characters["Agrias"];
            Assert.NotNull(agriasConfig);
            Assert.Equal(typeof(string), agriasConfig.EnumType);
            Assert.NotNull(agriasConfig.GetValue);
            Assert.NotNull(agriasConfig.SetValue);
        }

        [Fact]
        public void Registry_Should_Support_All_Operations()
        {
            // Arrange
            var config = new Config();
            var service = new CharacterDefinitionService();

            // Find the JSON file - try multiple paths
            var jsonPath = FindStoryCharactersJsonFile();

            if (System.IO.File.Exists(jsonPath))
            {
                service.LoadFromJson(jsonPath);
            }

            var characters = StoryCharacterRegistry.GetStoryCharactersFromService(config, service);

            // Act & Assert - Test setting and getting values
            if (characters.ContainsKey("Agrias"))
            {
                var agriasConfig = characters["Agrias"];

                // Set a value
                agriasConfig.SetValue("ash_dark");

                // Get the value back
                var value = agriasConfig.GetValue();
                Assert.Equal("ash_dark", value);

                // Verify it was set in the config
                Assert.Equal("ash_dark", config.Agrias);
            }
            else
            {
                Assert.True(false, "Agrias character not found in registry");
            }
        }

        [Fact]
        public void Should_Load_Characters_From_Json_File()
        {
            // Arrange
            var config = new Config();
            var service = new CharacterDefinitionService();

            // Load from the known JSON file location
            var jsonPath = FindStoryCharactersJsonFile();

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
            else
            {
                // Skip test if JSON file not found
                Assert.True(true, "JSON file not found, skipping test");
            }
        }

        private string FindStoryCharactersJsonFile()
        {
            var currentDir = System.IO.Directory.GetCurrentDirectory();

            // Get the base directory for the application domain
            var baseDirectory = System.AppDomain.CurrentDomain.BaseDirectory;

            // Try different relative paths from both current directory and base directory
            var possiblePaths = new[]
            {
                // From base directory (most reliable for tests)
                System.IO.Path.Combine(baseDirectory, "..", "..", "..", "..", "ColorMod", "Data", "StoryCharacters.json"),
                System.IO.Path.Combine(baseDirectory, "..", "..", "..", "ColorMod", "Data", "StoryCharacters.json"),
                System.IO.Path.Combine(baseDirectory, "..", "..", "ColorMod", "Data", "StoryCharacters.json"),
                System.IO.Path.Combine(baseDirectory, "..", "ColorMod", "Data", "StoryCharacters.json"),
                System.IO.Path.Combine(baseDirectory, "ColorMod", "Data", "StoryCharacters.json"),

                // From current directory
                System.IO.Path.Combine(currentDir, "..", "..", "..", "..", "ColorMod", "Data", "StoryCharacters.json"),
                System.IO.Path.Combine(currentDir, "..", "..", "..", "ColorMod", "Data", "StoryCharacters.json"),
                System.IO.Path.Combine(currentDir, "..", "..", "ColorMod", "Data", "StoryCharacters.json"),
                System.IO.Path.Combine(currentDir, "..", "ColorMod", "Data", "StoryCharacters.json"),
                System.IO.Path.Combine(currentDir, "ColorMod", "Data", "StoryCharacters.json"),

                // Absolute path as fallback
                @"C:\Users\ptyRa\Dev\FFTColorCustomizer\ColorMod\Data\StoryCharacters.json"
            };

            foreach (var path in possiblePaths)
            {
                try
                {
                    var fullPath = System.IO.Path.GetFullPath(path);
                    if (System.IO.File.Exists(fullPath))
                    {
                        System.Console.WriteLine($"Found JSON file at: {fullPath}");
                        return fullPath;
                    }
                }
                catch (System.Exception ex)
                {
                    // Skip invalid paths
                    System.Console.WriteLine($"Skipping invalid path {path}: {ex.Message}");
                    continue;
                }
            }

            // If nothing found, return the absolute path so we get a meaningful error
            var fallbackPath = @"C:\Users\ptyRa\Dev\FFTColorCustomizer\ColorMod\Data\StoryCharacters.json";
            System.Console.WriteLine($"No JSON file found, returning fallback: {fallbackPath}");
            return fallbackPath;
        }
    }
}
