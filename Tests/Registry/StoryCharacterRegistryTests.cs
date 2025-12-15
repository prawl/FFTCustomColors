using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;
using ColorMod.Registry;

namespace Tests.Registry
{
    [Collection("RegistryTests")]
    public class StoryCharacterRegistryTests : IDisposable
    {
        private static readonly object _testLock = new object();

        public StoryCharacterRegistryTests()
        {
            Monitor.Enter(_testLock);
            // Clear registry before each test
            StoryCharacterRegistry.Clear();
        }

        public void Dispose()
        {
            StoryCharacterRegistry.Clear();
            Monitor.Exit(_testLock);
        }

        [Fact]
        public void Registry_ShouldRegisterCharacterDefinition()
        {
            // Arrange
            var definition = new StoryCharacterDefinition
            {
                Name = "TestCharacter",
                EnumType = typeof(TestColorScheme),
                SpriteNames = new[] { "test_sprite" },
                DefaultTheme = "default"
            };

            // Act
            StoryCharacterRegistry.Register(definition);

            // Assert
            Assert.True(StoryCharacterRegistry.HasCharacter("TestCharacter"));
            var retrieved = StoryCharacterRegistry.GetCharacter("TestCharacter");
            Assert.Equal("TestCharacter", retrieved.Name);
            Assert.Equal(typeof(TestColorScheme), retrieved.EnumType);
            Assert.Equal(new[] { "test_sprite" }, retrieved.SpriteNames);
            Assert.Equal("default", retrieved.DefaultTheme);
        }

        [Fact]
        public void Registry_ShouldAutoDiscoverCharactersWithAttribute()
        {
            // Act - Auto-discover all character enums with the StoryCharacter attribute
            StoryCharacterRegistry.AutoDiscoverCharacters();

            // Debug - let's see what was registered
            var registeredCount = StoryCharacterRegistry.GetAllCharacterNames().Count();

            // Assert - We should have found at least AttributeTest
            Assert.True(registeredCount > 0, $"No characters found. Registry count: {registeredCount}");
            Assert.True(StoryCharacterRegistry.HasCharacter("AttributeTest"),
                $"AttributeTest not found. Registered characters: {string.Join(", ", StoryCharacterRegistry.GetAllCharacterNames())}");

            var character = StoryCharacterRegistry.GetCharacter("AttributeTest");
            Assert.Equal("AttributeTest", character.Name);
            Assert.Equal(typeof(AttributeTestColorScheme), character.EnumType);
            Assert.Equal(new[] { "test_sprite1", "test_sprite2" }, character.SpriteNames);
            Assert.Equal("default_theme", character.DefaultTheme);
        }

        // Test enum for testing purposes
        public enum TestColorScheme
        {
            default_theme,
            alternative_theme
        }

        // Test enum with attribute for auto-discovery
        [StoryCharacter(SpriteNames = new[] { "test_sprite1", "test_sprite2" }, DefaultTheme = "default_theme")]
        public enum AttributeTestColorScheme
        {
            default_theme,
            alternative_theme,
            special_theme
        }
    }
}