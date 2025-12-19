using System;
using System.IO;
using System.Linq;
using Xunit;
using ColorMod.Registry;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Utilities;
using FFTColorCustomizer.Services;

namespace Tests.Registry
{
    [Collection("RegistryTests")]
    public class ExistingCharacterMigrationTests : IDisposable
    {
        private StoryCharacterThemeManager _themeManager;
        private CharacterDefinitionService _characterService;
        private static readonly object _testLock = new object();

        public ExistingCharacterMigrationTests()
        {
            System.Threading.Monitor.Enter(_testLock);
            _characterService = new CharacterDefinitionService();
            _themeManager = new StoryCharacterThemeManager();
            StoryCharacterRegistry.Clear();

            // Load characters from JSON file
            LoadCharactersFromJson();
        }

        public void Dispose()
        {
            StoryCharacterRegistry.Clear();
            System.Threading.Monitor.Exit(_testLock);
        }

        private void LoadCharactersFromJson()
        {
            // Use absolute path to the known JSON file location
            var jsonPath = Path.Combine(ColorModConstants.DevSourcePath, ColorModConstants.DataDirectory, ColorModConstants.StoryCharactersFile);

            if (File.Exists(jsonPath))
            {
                _characterService.LoadFromJson(jsonPath);

                // Register characters in the old registry for backward compatibility
                foreach (var character in _characterService.GetAllCharacters())
                {
                    var definition = new StoryCharacterDefinition
                    {
                        Name = character.Name,
                        EnumType = typeof(string), // Updated to string
                        SpriteNames = character.SpriteNames,
                        DefaultTheme = character.DefaultTheme
                    };
                    StoryCharacterRegistry.Register(definition);
                }
            }
        }

        [Fact]
        public void CloudCharacter_ShouldBeDiscoverable_FromJson()
        {
            // Assert - Cloud should be registered
            Assert.True(StoryCharacterRegistry.HasCharacter("Cloud"),
                "Cloud character not found. Available: " + string.Join(", ", StoryCharacterRegistry.GetAllCharacterNames()));

            var cloud = StoryCharacterRegistry.GetCharacter("Cloud");
            Assert.Equal("Cloud", cloud.Name);
            Assert.Equal(typeof(string), cloud.EnumType);
            Assert.Contains("cloud", cloud.SpriteNames);
            Assert.Equal("original", cloud.DefaultTheme); // Updated to match JSON
        }

        [Fact]
        public void CloudThemeCycling_ShouldWork_WithNewSystem()
        {
            // Act - Cycle through Cloud themes (Cloud now has 'original', 'holy_soldier', 'young_soldier', etc. in JSON)
            var theme1 = _themeManager.GetCurrentTheme("Cloud");
            var theme2 = _themeManager.CycleTheme("Cloud");
            var theme3 = _themeManager.CycleTheme("Cloud");
            var theme4 = _themeManager.CycleTheme("Cloud");

            // Assert - Cloud has 7 themes available in JSON
            Assert.Equal("original", theme1); // Default
            Assert.Equal("holy_soldier", theme2); // Second theme
            Assert.Equal("young_soldier", theme3); // Third theme
            Assert.Equal("sky_pirate", theme4); // Fourth theme
        }

        [Fact]
        public void AgriasCharacter_ShouldBeDiscoverable_FromJson()
        {
            // Assert - Agrias should be registered
            Assert.True(StoryCharacterRegistry.HasCharacter("Agrias"),
                "Agrias character not found. Available: " + string.Join(", ", StoryCharacterRegistry.GetAllCharacterNames()));

            var agrias = StoryCharacterRegistry.GetCharacter("Agrias");
            Assert.Equal("Agrias", agrias.Name);
            Assert.Equal(typeof(string), agrias.EnumType);
            Assert.Contains("aguri", agrias.SpriteNames);
            Assert.Contains("kanba", agrias.SpriteNames);
            Assert.Equal("original", agrias.DefaultTheme);
        }
    }
}
