using System;
using System.IO;
using System.Linq;
using Xunit;
using FFTColorMod.Configuration;
using FFTColorMod.Configuration.UI;
using FFTColorMod.Services;
using FFTColorMod.Utilities;

namespace Tests.Integration
{
    public class RefactoredSystemIntegrationTests : IDisposable
    {
        private readonly string _testPath;
        private readonly ConfigurationManager _configManager;

        public RefactoredSystemIntegrationTests()
        {
            _testPath = Path.Combine(Path.GetTempPath(), "IntegrationTest_" + Guid.NewGuid());
            Directory.CreateDirectory(_testPath);

            var configPath = Path.Combine(_testPath, "Config.json");
            _configManager = new ConfigurationManager(configPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testPath))
                Directory.Delete(_testPath, true);
        }

        [Fact]
        public void Full_System_Should_Work_End_To_End()
        {
            // Arrange - Setup the complete refactored system
            var characterService = new CharacterDefinitionService();
            var sourceUnitPath = Path.Combine(_testPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            var spriteResolver = new ConventionBasedSpriteResolver(sourceUnitPath);

            // Load character definitions (or add them manually for test)
            characterService.AddCharacter(new CharacterDefinition
            {
                Name = "Agrias",
                SpriteNames = new[] { "aguri", "kanba" },
                DefaultTheme = "original",
                AvailableThemes = new[] { "original", "ash_dark" },
                EnumType = "AgriasColorScheme"
            });

            characterService.AddCharacter(new CharacterDefinition
            {
                Name = "Cloud",
                SpriteNames = new[] { "cloud" },
                DefaultTheme = "original",
                AvailableThemes = new[] { "original", "dark" },
                EnumType = "CloudColorScheme"
            });

            // Setup test sprite files - need to be in the source unit path
            Directory.CreateDirectory(sourceUnitPath);

            var agriasThemeDir = Path.Combine(sourceUnitPath, "sprites_agrias_ash_dark");
            Directory.CreateDirectory(agriasThemeDir);
            File.WriteAllText(Path.Combine(agriasThemeDir, "battle_aguri_spr.bin"), "agrias_ash_dark");

            var cloudThemeDir = Path.Combine(sourceUnitPath, "sprites_cloud_dark");
            Directory.CreateDirectory(cloudThemeDir);
            File.WriteAllText(Path.Combine(cloudThemeDir, "battle_cloud_spr.bin"), "cloud_dark");

            // Act - Test the complete flow
            // 1. Get characters from service
            var characters = characterService.GetAllCharacters();
            Assert.Equal(2, characters.Count);

            // 2. Use ConventionBasedSpriteResolver to find sprites
            var agriasSprite = spriteResolver.ResolveSpriteTheme("agrias", "aguri", "ash_dark");
            Assert.NotNull(agriasSprite);
            Assert.True(File.Exists(agriasSprite));

            // 3. Discover available themes
            var agriasThemes = spriteResolver.DiscoverAvailableThemes("agrias", "aguri");
            Assert.Contains("ash_dark", agriasThemes);

            // 4. Use refactored registry with config
            var config = new Config();
            StoryCharacterRegistry.AutoDiscoverCharacters(characterService);
            var registryCharacters = StoryCharacterRegistry.GetStoryCharactersFromService(config, characterService);
            Assert.True(registryCharacters.Count > 0);

            // 5. Verify the complete system can apply configurations
            config.Agrias = AgriasColorScheme.ash_dark;
            _configManager.SaveConfig(config);

            // Create destination directory for sprite manager
            var unitPath = Path.Combine(_testPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(unitPath);

            var spriteManager = new ConfigBasedSpriteManager(_testPath, _configManager, characterService, _testPath);
            spriteManager.ApplyConfiguration();

            // Verify sprite was copied
            var destSprite = Path.Combine(unitPath, "battle_aguri_spr.bin");
            Assert.True(File.Exists(destSprite), "Sprite should be copied by sprite manager");
        }

        [Fact]
        public void System_Should_Handle_New_Character_Addition_Easily()
        {
            // This test demonstrates how easy it is to add a new character with the refactored system

            // Arrange
            var characterService = new CharacterDefinitionService();

            // Act - Adding a new character requires just adding to the service
            var newCharacter = new CharacterDefinition
            {
                Name = "NewHero",
                SpriteNames = new[] { "newhero_sprite" },
                DefaultTheme = "original",
                AvailableThemes = new[] { "original", "special" },
                EnumType = "NewHeroColorScheme"
            };
            characterService.AddCharacter(newCharacter);

            // Assert - Character is immediately available everywhere
            var allCharacters = characterService.GetAllCharacters();
            Assert.Contains(allCharacters, c => c.Name == "NewHero");

            var foundCharacter = characterService.GetCharacterByName("NewHero");
            Assert.NotNull(foundCharacter);
            Assert.Equal("NewHero", foundCharacter.Name);

            // No need to modify multiple files - just the service!
        }

        [Fact]
        public void System_Should_Auto_Discover_Themes_From_File_System()
        {
            // Arrange
            var spriteResolver = new ConventionBasedSpriteResolver(_testPath);

            // Create multiple theme directories for testing
            Directory.CreateDirectory(Path.Combine(_testPath, "sprites_testchar_theme1"));
            Directory.CreateDirectory(Path.Combine(_testPath, "sprites_testchar_theme2"));
            Directory.CreateDirectory(Path.Combine(_testPath, "sprites_testchar_theme3"));

            // Also add a flat file theme
            File.WriteAllText(Path.Combine(_testPath, "battle_testsprite_theme4_spr.bin"), "test");

            // Act
            var themes = spriteResolver.DiscoverAvailableThemes("testchar", "testsprite");

            // Assert
            Assert.Contains("theme1", themes);
            Assert.Contains("theme2", themes);
            Assert.Contains("theme3", themes);
            Assert.Contains("theme4", themes);
            Assert.Equal(4, themes.Count);
        }
    }
}