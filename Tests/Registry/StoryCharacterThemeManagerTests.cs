using System;
using Xunit;
using ColorMod.Registry;

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
            var theme = _manager.GetTheme<TestColorScheme>("TestCharacter");

            // Assert
            Assert.Equal(TestColorScheme.alternative, theme);
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
            var firstTheme = _manager.GetTheme<TestColorScheme>("TestCharacter");
            var secondTheme = _manager.CycleTheme<TestColorScheme>("TestCharacter");
            var thirdTheme = _manager.CycleTheme<TestColorScheme>("TestCharacter");

            // Assert
            Assert.Equal(TestColorScheme.original, firstTheme);
            Assert.Equal(TestColorScheme.alternative, secondTheme);
            Assert.Equal(TestColorScheme.special, thirdTheme);
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
            _manager.GetTheme<TestColorScheme>("TestCharacter"); // Start at special
            var nextTheme = _manager.CycleTheme<TestColorScheme>("TestCharacter");

            // Assert - should wrap to first enum value
            Assert.Equal(TestColorScheme.original, nextTheme);
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
            _manager.SetTheme("TestCharacter", TestColorScheme.special);
            var currentTheme = _manager.GetTheme<TestColorScheme>("TestCharacter");

            // Assert
            Assert.Equal(TestColorScheme.special, currentTheme);
        }

        public enum TestColorScheme
        {
            original,
            alternative,
            special
        }
    }
}