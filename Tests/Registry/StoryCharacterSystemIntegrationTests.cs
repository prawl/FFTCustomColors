using System;
using System.IO;
using Xunit;
using ColorMod.Registry;
using FFTColorMod.Utilities;

namespace Tests.Registry
{
    /// <summary>
    /// Integration test demonstrating how the new streamlined system works:
    /// 1. Define enum with attribute (replaces steps 3-8 in original manual process)
    /// 2. Create theme directories with sprites (steps 1-2 remain manual)
    /// 3. System auto-wires everything else!
    /// </summary>
    [Collection("RegistryTests")]
    public class StoryCharacterSystemIntegrationTests : IDisposable
    {
        private StoryCharacterThemeManager _themeManager;
        private static readonly object _testLock = new object();

        public StoryCharacterSystemIntegrationTests()
        {
            System.Threading.Monitor.Enter(_testLock);
            _themeManager = new StoryCharacterThemeManager();
            StoryCharacterRegistry.Clear();
        }

        public void Dispose()
        {
            StoryCharacterRegistry.Clear();
            System.Threading.Monitor.Exit(_testLock);
        }

        [Fact]
        public void NewStoryCharacter_WithOnlyEnumAndAttribute_ShouldBeFullyFunctional()
        {
            // This test demonstrates the streamlined approach:
            // Just add this enum with attribute, and everything auto-wires!

            // Act - Auto-discover the new character
            StoryCharacterRegistry.AutoDiscoverCharacters();

            // Assert - The character is fully registered and functional
            Assert.True(StoryCharacterRegistry.HasCharacter("NewCharacter"));

            var character = StoryCharacterRegistry.GetCharacter("NewCharacter");
            Assert.Equal("NewCharacter", character.Name);
            Assert.Equal(typeof(NewCharacterColorScheme), character.EnumType);
            Assert.Equal(new[] { "new_char_sprite1", "new_char_sprite2" }, character.SpriteNames);
            Assert.Equal("lightning_blue", character.DefaultTheme);

            // The theme manager works automatically
            var currentTheme = _themeManager.GetCurrentTheme("NewCharacter");
            Assert.Equal("lightning_blue", currentTheme);

            // Cycling works automatically
            var nextTheme = _themeManager.CycleTheme("NewCharacter");
            Assert.Equal("shadow_black", nextTheme);
        }

        [Fact]
        public void StoryCharacterSpriteResolver_ShouldMapSpritesToCharacter()
        {
            // Arrange
            StoryCharacterRegistry.AutoDiscoverCharacters();
            var resolver = new StoryCharacterSpriteResolver();

            // Act - Check if sprite names map to the correct character
            var character1 = resolver.GetCharacterForSprite("new_char_sprite1");
            var character2 = resolver.GetCharacterForSprite("new_char_sprite2");

            // Assert
            Assert.Equal("NewCharacter", character1);
            Assert.Equal("NewCharacter", character2);
        }

        [Fact]
        public void CompleteWorkflow_AddingNewCharacter_RequiresMinimalSteps()
        {
            // This test documents the complete workflow for adding a new character

            // Step 1: Create the enum with attribute (replaces original steps 3-8)
            // See NewCharacterColorScheme below

            // Step 2: Auto-discover (can be done once at startup)
            StoryCharacterRegistry.AutoDiscoverCharacters();

            // That's it! The character is now fully integrated:

            // - Registry knows about it
            Assert.True(StoryCharacterRegistry.HasCharacter("NewCharacter"));

            // - Theme cycling works
            var theme = _themeManager.GetCurrentTheme("NewCharacter");
            Assert.NotNull(theme);

            // - Sprite mapping works
            var resolver = new StoryCharacterSpriteResolver();
            Assert.Equal("NewCharacter", resolver.GetCharacterForSprite("new_char_sprite1"));

            // Compare to original process:
            // ELIMINATED: Step 3 - Add enum to Mod.cs
            // ELIMINATED: Step 4 - Add CurrentTheme tracking
            // ELIMINATED: Step 5 - Add to CycleTheme methods
            // ELIMINATED: Step 6 - Add to InterceptFilePath
            // ELIMINATED: Step 7 - Add to ApplyInitialThemes
            // ELIMINATED: Step 8 - Update build script

            // Only Steps 1-2 remain (create sprites/directories) which are inherently manual
        }

        // This single enum definition replaces steps 3-8 of the original manual process!
        [StoryCharacter(SpriteNames = new[] { "new_char_sprite1", "new_char_sprite2" }, DefaultTheme = "lightning_blue")]
        public enum NewCharacterColorScheme
        {
            lightning_blue,
            shadow_black,
            crystal_white,
            fire_red
        }
    }

    /// <summary>
    /// Helper class to resolve sprite names to characters
    /// </summary>
    public class StoryCharacterSpriteResolver
    {
        public string GetCharacterForSprite(string spriteName)
        {
            foreach (var characterName in StoryCharacterRegistry.GetAllCharacterNames())
            {
                var character = StoryCharacterRegistry.GetCharacter(characterName);
                foreach (var sprite in character.SpriteNames)
                {
                    if (sprite == spriteName)
                        return characterName;
                }
            }
            return null;
        }
    }
}