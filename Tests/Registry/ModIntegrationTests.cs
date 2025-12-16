using System;
using System.IO;
using Xunit;
using ColorMod.Registry;
using FFTColorMod;
using FFTColorMod.Configuration;
using FFTColorMod.Services;

namespace Tests.Registry
{
    /// <summary>
    /// Tests for integrating the registry system with Mod.cs
    /// </summary>
    [Collection("RegistryTests")]
    public class ModIntegrationTests : IDisposable
    {
        private static readonly object _testLock = new object();

        public ModIntegrationTests()
        {
            System.Threading.Monitor.Enter(_testLock);
            StoryCharacterRegistry.Clear();
        }

        public void Dispose()
        {
            StoryCharacterRegistry.Clear();
            System.Threading.Monitor.Exit(_testLock);
        }

        [Fact]
        public void Mod_Constructor_ShouldInitializeRegistry()
        {
            // Arrange
            var context = new ModContext();

            // Act
            var mod = new Mod(context);

            // Assert - All story characters should be registered
            Assert.True(StoryCharacterRegistry.HasCharacter("Cloud"));
            Assert.True(StoryCharacterRegistry.HasCharacter("Agrias"));
            Assert.True(StoryCharacterRegistry.HasCharacter("Orlandeau"));

            // Verify character definitions are correct
            var cloud = StoryCharacterRegistry.GetCharacter("Cloud");
            Assert.NotNull(cloud);
            Assert.NotNull(cloud.EnumType);
            Assert.Contains("cloud", cloud.SpriteNames);

            var agrias = StoryCharacterRegistry.GetCharacter("Agrias");
            Assert.NotNull(agrias);
            Assert.NotNull(agrias.EnumType);
            Assert.Contains("aguri", agrias.SpriteNames);

            var orlandeau = StoryCharacterRegistry.GetCharacter("Orlandeau");
            Assert.NotNull(orlandeau);
            Assert.NotNull(orlandeau.EnumType);
            Assert.Contains("oru", orlandeau.SpriteNames);
            Assert.Contains("goru", orlandeau.SpriteNames);
        }

        [Fact]
        public void Mod_InitializeConfiguration_ShouldSetThemesFromConfig()
        {
            // Arrange
            var context = new ModContext();
            var mod = new Mod(context);

            // Create a temp config file with specific themes
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var configPath = Path.Combine(tempDir, "Config.json");

            var config = new Config
            {
                Cloud = "original",
                Agrias = "ash_dark",
                Orlandeau = "thunder_god"
            };

            var configJson = Newtonsoft.Json.JsonConvert.SerializeObject(config);
            File.WriteAllText(configPath, configJson);

            try
            {
                // Act
                mod.InitializeConfiguration(configPath);

                // Assert - Theme manager should be initialized with config values
                var themeManager = mod.GetThemeManager();
                Assert.NotNull(themeManager);

                var storyManager = themeManager.GetStoryCharacterManager();
                Assert.NotNull(storyManager);

                // Themes should match config (using old implementation methods)
                Assert.Equal("original", storyManager.GetCurrentTheme("Cloud"));
                Assert.Equal("ash_dark", storyManager.GetCurrentTheme("Agrias"));
                Assert.Equal("thunder_god", storyManager.GetCurrentTheme("Orlandeau"));
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}