using System;
using Xunit;
using ColorMod.Registry;
using FFTColorMod.Utilities;

namespace Tests.Registry
{
    [Collection("RegistryTests")]
    public class StoryCharacterThemeManagerTests : IDisposable
    {
        private StoryCharacterThemeManager _manager;
        private static readonly object _testLock = new object();

        public StoryCharacterThemeManagerTests()
        {
            System.Threading.Monitor.Enter(_testLock);
            _manager = new StoryCharacterThemeManager();
            StoryCharacterRegistry.Clear();
        }

        public void Dispose()
        {
            StoryCharacterRegistry.Clear();
            System.Threading.Monitor.Exit(_testLock);
        }

        [Fact]
        public void GetTheme_ShouldReturnDefaultTheme_WhenNeverSet()
        {
            // Arrange
            var definition = new StoryCharacterDefinition
            {
                Name = "TestCharacter",
                EnumType = typeof(TestColorScheme),
                SpriteNames = new[] { "test_sprite" },
                DefaultTheme = "alternative"
            };
            StoryCharacterRegistry.Register(definition);

            // Act
            var theme = _manager.GetCurrentTheme("TestCharacter");

            // Assert
            Assert.Equal("alternative", theme);
        }

        [Fact]
        public void CycleTheme_ShouldMoveToNextTheme()
        {
            // Arrange
            var definition = new StoryCharacterDefinition
            {
                Name = "TestCharacter",
                EnumType = typeof(TestColorScheme),
                SpriteNames = new[] { "test_sprite" },
                DefaultTheme = "original"
            };
            StoryCharacterRegistry.Register(definition);

            // Act
            var firstTheme = _manager.GetCurrentTheme("TestCharacter");
            var secondTheme = _manager.CycleTheme("TestCharacter");
            var thirdTheme = _manager.CycleTheme("TestCharacter");

            // Assert
            Assert.Equal("original", firstTheme);
            Assert.Equal("alternative", secondTheme);
            Assert.Equal("special", thirdTheme);
        }

        [Fact]
        public void CycleTheme_ShouldWrapAroundToFirst()
        {
            // Arrange
            var definition = new StoryCharacterDefinition
            {
                Name = "TestCharacter",
                EnumType = typeof(TestColorScheme),
                SpriteNames = new[] { "test_sprite" },
                DefaultTheme = "special"
            };
            StoryCharacterRegistry.Register(definition);

            // Act
            _manager.GetCurrentTheme("TestCharacter"); // Start at special
            var nextTheme = _manager.CycleTheme("TestCharacter");

            // Assert - should wrap to first enum value
            Assert.Equal("original", nextTheme);
        }

        [Fact]
        public void SetTheme_ShouldUpdateThemeDirectly()
        {
            // Arrange
            var definition = new StoryCharacterDefinition
            {
                Name = "TestCharacter",
                EnumType = typeof(TestColorScheme),
                SpriteNames = new[] { "test_sprite" },
                DefaultTheme = "original"
            };
            StoryCharacterRegistry.Register(definition);

            // Act
            _manager.SetCurrentTheme("TestCharacter", "special");
            var currentTheme = _manager.GetCurrentTheme("TestCharacter");

            // Assert
            Assert.Equal("special", currentTheme);
        }

        public enum TestColorScheme
        {
            original,
            alternative,
            special
        }
    }
}