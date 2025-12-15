using System;
using System.Linq;
using Xunit;
using ColorMod.Registry;
using FFTColorMod.Configuration;

namespace Tests.Registry
{
    [Collection("RegistryTests")]
    public class ExistingCharacterMigrationTests : IDisposable
    {
        private StoryCharacterThemeManager _themeManager;
        private static readonly object _testLock = new object();

        public ExistingCharacterMigrationTests()
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
        public void CloudCharacter_ShouldBeDiscoverable_WithAttribute()
        {
            // Act - Auto-discover Cloud
            StoryCharacterRegistry.AutoDiscoverCharacters();

            // Assert - Cloud should be registered
            Assert.True(StoryCharacterRegistry.HasCharacter("Cloud"),
                "Cloud character not found. Available: " + string.Join(", ", StoryCharacterRegistry.GetAllCharacterNames()));

            var cloud = StoryCharacterRegistry.GetCharacter("Cloud");
            Assert.Equal("Cloud", cloud.Name);
            Assert.Equal(typeof(CloudColorScheme), cloud.EnumType);
            Assert.Contains("cloud", cloud.SpriteNames);
            Assert.Equal("sephiroth_black", cloud.DefaultTheme);
        }

        [Fact]
        public void CloudThemeCycling_ShouldWork_WithNewSystem()
        {
            // Arrange
            StoryCharacterRegistry.AutoDiscoverCharacters();

            // Act - Cycle through Cloud themes
            var theme1 = _themeManager.GetTheme<CloudColorScheme>("Cloud");
            var theme2 = _themeManager.CycleTheme<CloudColorScheme>("Cloud");
            var theme3 = _themeManager.CycleTheme<CloudColorScheme>("Cloud");
            var theme4 = _themeManager.CycleTheme<CloudColorScheme>("Cloud"); // Should wrap

            // Assert
            Assert.Equal(CloudColorScheme.sephiroth_black, theme1); // Default
            Assert.Equal(CloudColorScheme.original, theme2);
            Assert.Equal(CloudColorScheme.knights_round, theme3);
            Assert.Equal(CloudColorScheme.sephiroth_black, theme4); // Wrapped
        }

        [Fact]
        public void AgriasCharacter_ShouldBeDiscoverable_WithAttribute()
        {
            // Act - Auto-discover Agrias
            StoryCharacterRegistry.AutoDiscoverCharacters();

            // Assert - Agrias should be registered
            Assert.True(StoryCharacterRegistry.HasCharacter("Agrias"),
                "Agrias character not found. Available: " + string.Join(", ", StoryCharacterRegistry.GetAllCharacterNames()));

            var agrias = StoryCharacterRegistry.GetCharacter("Agrias");
            Assert.Equal("Agrias", agrias.Name);
            Assert.Equal(typeof(AgriasColorScheme), agrias.EnumType);
            Assert.Contains("aguri", agrias.SpriteNames);
            Assert.Contains("kanba", agrias.SpriteNames);
            Assert.Equal("original", agrias.DefaultTheme);
        }
    }
}