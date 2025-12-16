using System;
using System.Reflection;
using Xunit;
using ColorMod.Registry;
using FFTColorMod.Configuration;

namespace Tests.Registry
{
    /// <summary>
    /// Tests for refactoring Mod.cs to use the new registry system
    /// </summary>
    [Collection("RegistryTests")]
    public class ModRefactoringTests : IDisposable
    {
        private StoryCharacterThemeManager _themeManager;
        private static readonly object _testLock = new object();

        public ModRefactoringTests()
        {
            System.Threading.Monitor.Enter(_testLock);
            _themeManager = new StoryCharacterThemeManager();
            StoryCharacterRegistry.Clear();
            StoryCharacterRegistry.AutoDiscoverCharacters();
        }

        public void Dispose()
        {
            StoryCharacterRegistry.Clear();
            System.Threading.Monitor.Exit(_testLock);
        }

        [Theory]
        [InlineData("battle_cloud_spr.bin", "Cloud")]
        [InlineData("battle_aguri_spr.bin", "Agrias")]
        [InlineData("battle_kanba_spr.bin", "Agrias")]
        [InlineData("battle_oru_spr.bin", "Orlandeau")]
        [InlineData("battle_goru_spr.bin", "Orlandeau")]
        [InlineData("battle_voru_spr.bin", null)]
        public void SpriteResolver_ShouldMapSpritesToCharacters(string spriteName, string expectedCharacter)
        {
            // Arrange
            var resolver = new StoryCharacterSpriteResolver();

            // Act
            var character = resolver.GetCharacterForSprite(GetSpriteNameWithoutExtension(spriteName));

            // Assert
            Assert.Equal(expectedCharacter, character);
        }

        [Fact]
        public void InterceptFilePath_ShouldUseRegistry_ForStoryCharacters()
        {
            // Arrange
            var interceptor = new SpritePathInterceptor(_themeManager);
            _themeManager.SetTheme("Cloud", CloudColorScheme.knights_round);

            // Act
            var cloudPath = interceptor.InterceptPath("data/sprites/battle_cloud_spr.bin");

            // Assert
            Assert.Contains("sprites_cloud_knights_round", cloudPath);
            Assert.EndsWith("battle_cloud_spr.bin", cloudPath);
        }

        [Fact]
        public void F1KeyPress_ShouldCycleThemes_UsingThemeManager()
        {
            // Arrange
            var cycler = new ThemeCycler(_themeManager);

            // Act - Simulate F1 key press (cycles story characters)
            cycler.CycleStoryCharacterThemes();

            // Assert
            var cloudTheme = _themeManager.GetTheme<CloudColorScheme>("Cloud");
            var agriasTheme = _themeManager.GetTheme<AgriasColorScheme>("Agrias");

            // Should have cycled from default
            Assert.NotEqual(CloudColorScheme.sephiroth_black, cloudTheme);
            Assert.NotEqual(AgriasColorScheme.original, agriasTheme);
        }

        [Fact]
        public void OrlandeauCharacter_ShouldBeRegistered_WithAllSprites()
        {
            // Assert
            Assert.True(StoryCharacterRegistry.HasCharacter("Orlandeau"));
            var orlandeau = StoryCharacterRegistry.GetCharacter("Orlandeau");

            // Orlandeau uses two sprite variants
            Assert.Contains("oru", orlandeau.SpriteNames);
            Assert.Contains("goru", orlandeau.SpriteNames);
        }

        private string GetSpriteNameWithoutExtension(string fileName)
        {
            // Extract sprite name from "battle_XXX_spr.bin" format
            var start = fileName.IndexOf("battle_") + 7;
            var end = fileName.IndexOf("_spr.bin");
            return fileName.Substring(start, end - start);
        }
    }


    /// <summary>
    /// Helper to intercept sprite paths using the registry
    /// </summary>
    public class SpritePathInterceptor
    {
        private readonly StoryCharacterThemeManager _themeManager;
        private readonly Tests.Registry.StoryCharacterSpriteResolver _resolver;

        public SpritePathInterceptor(StoryCharacterThemeManager themeManager)
        {
            _themeManager = themeManager;
            _resolver = new Tests.Registry.StoryCharacterSpriteResolver();
        }

        public string InterceptPath(string originalPath)
        {
            if (!originalPath.Contains("battle_") || !originalPath.EndsWith("_spr.bin"))
                return originalPath;

            var fileName = System.IO.Path.GetFileName(originalPath);
            var spriteName = GetSpriteNameFromFile(fileName);
            var character = _resolver.GetCharacterForSprite(spriteName);

            if (character == null)
                return originalPath;

            var characterDef = StoryCharacterRegistry.GetCharacter(character);
            var currentTheme = _themeManager.GetThemeString(character);

            // Build new path: sprites_[character]_[theme]/[filename]
            var themePath = $"sprites_{character.ToLower()}_{currentTheme}/{fileName}";
            return originalPath.Replace($"sprites/{fileName}", themePath);
        }

        private string GetSpriteNameFromFile(string fileName)
        {
            var start = fileName.IndexOf("battle_") + 7;
            var end = fileName.IndexOf("_spr.bin");
            return fileName.Substring(start, end - start);
        }
    }

    /// <summary>
    /// Helper to cycle themes
    /// </summary>
    public class ThemeCycler
    {
        private readonly StoryCharacterThemeManager _themeManager;

        public ThemeCycler(StoryCharacterThemeManager themeManager)
        {
            _themeManager = themeManager;
        }

        public void CycleStoryCharacterThemes()
        {
            foreach (var characterName in StoryCharacterRegistry.GetAllCharacterNames())
            {
                _themeManager.CycleThemeGeneric(characterName);
            }
        }
    }
}