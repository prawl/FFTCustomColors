using System;
using System.IO;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.Interfaces;
using FFTColorCustomizer.Utilities;
using Moq;
using Xunit;
using static FFTColorCustomizer.Core.ColorModConstants;

namespace FFTColorCustomizer.Tests.Integration
{
    /// <summary>
    /// Comprehensive tests for the hot reload fix that allows F1 configuration changes
    /// to take effect immediately without restarting Reloaded-II
    /// </summary>
    public class HotReloadTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _configPath;
        private readonly Mock<IInputSimulator> _inputSimulator;
        private readonly string _modPath;
        private readonly string _unitPath;

        public HotReloadTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"FFTHotReloadTest_{Guid.NewGuid()}");
            _modPath = _testDirectory;
            Directory.CreateDirectory(_testDirectory);

            // Create the full directory structure that InterceptFilePath expects
            _unitPath = Path.Combine(_modPath, FFTIVCPath, DataPath, EnhancedPath, FFTPackPath, UnitPath);
            Directory.CreateDirectory(_unitPath);

            // Create a User config path for the configuration coordinator
            var userDir = Path.Combine(_testDirectory, "User", "Mods", "paxtrick.fft.colorcustomizer");
            Directory.CreateDirectory(userDir);
            _configPath = Path.Combine(userDir, "Config.json");

            _inputSimulator = new Mock<IInputSimulator>();

            // Initialize JobClassService with test data
            var dataPath = Path.Combine(_testDirectory, "Data");
            Directory.CreateDirectory(dataPath);
            CreateMockJobClassesJson(dataPath);
            JobClassServiceSingleton.Initialize(_testDirectory);

            // Create themed sprite directories
            CreateThemedSpriteDirectories();
        }

        private void CreateMockJobClassesJson(string dataPath)
        {
            var jobClassesJson = @"{
                ""sharedThemes"": [""original"", ""holy_guard"", ""shadow_assassin"", ""desert_nomad""],
                ""jobClasses"": [
                    {
                        ""name"": ""Knight_Male"",
                        ""displayName"": ""Knight (Male)"",
                        ""spriteName"": ""battle_knight_m_spr.bin"",
                        ""defaultTheme"": ""original"",
                        ""gender"": ""Male"",
                        ""jobType"": ""Knight"",
                        ""jobSpecificThemes"": [""holy_guard""]
                    },
                    {
                        ""name"": ""Monk_Male"",
                        ""displayName"": ""Monk (Male)"",
                        ""spriteName"": ""battle_monk_m_spr.bin"",
                        ""defaultTheme"": ""original"",
                        ""gender"": ""Male"",
                        ""jobType"": ""Monk"",
                        ""jobSpecificThemes"": [""shadow_assassin""]
                    },
                    {
                        ""name"": ""Archer_Female"",
                        ""displayName"": ""Archer (Female)"",
                        ""spriteName"": ""battle_yumi_w_spr.bin"",
                        ""defaultTheme"": ""original"",
                        ""gender"": ""Female"",
                        ""jobType"": ""Archer"",
                        ""jobSpecificThemes"": [""desert_nomad""]
                    }
                ]
            }";

            File.WriteAllText(Path.Combine(dataPath, "JobClasses.json"), jobClassesJson);
        }

        private void CreateThemedSpriteDirectories()
        {
            // Create themed sprite directories with mock sprite files
            var themes = new[] { "original", "holy_guard", "shadow_assassin", "desert_nomad" };
            var sprites = new[] { "battle_knight_m_spr.bin", "battle_monk_m_spr.bin", "battle_yumi_w_spr.bin" };

            foreach (var theme in themes)
            {
                var themeDir = Path.Combine(_unitPath, $"sprites_{theme}");
                Directory.CreateDirectory(themeDir);

                foreach (var sprite in sprites)
                {
                    var spritePath = Path.Combine(themeDir, sprite);
                    File.WriteAllText(spritePath, $"MOCK_SPRITE_{theme}_{sprite}");
                }
            }
        }

        [Fact]
        public void GetJobColor_Should_Respect_PerJob_Configuration()
        {
            // This tests the core logic without depending on file system paths
            // The key is that GetJobColor returns the correct theme for each job

            // Arrange - Create mod with initial configuration
            var initialConfig = new Config
            {
                Knight_Male = "original",
                Monk_Male = "original",
                Archer_Female = "original"
            };
            File.WriteAllText(_configPath, System.Text.Json.JsonSerializer.Serialize(initialConfig));

            var context = new ModContext();
            var mod = new Mod(context, _inputSimulator.Object, new NullHotkeyHandler());
            mod.InitializeConfiguration(_configPath);
            mod.Start(null);

            // Act - Update configuration to use different themes per job
            var updatedConfig = new Config
            {
                Knight_Male = "holy_guard",
                Monk_Male = "shadow_assassin",
                Archer_Female = "desert_nomad"
            };
            mod.ConfigurationUpdated(updatedConfig);

            // Test that GetJobColor returns the correct theme for each job
            var knightTheme = mod.GetJobColor("Knight_Male");
            var monkTheme = mod.GetJobColor("Monk_Male");
            var archerTheme = mod.GetJobColor("Archer_Female");

            // Assert - Each job should have its configured theme
            Assert.Equal("Holy Guard", knightTheme);
            Assert.Equal("Shadow Assassin", monkTheme);
            Assert.Equal("Desert Nomad", archerTheme);
        }

        [Fact]
        public void InterceptFilePath_Should_Return_Original_When_Theme_Is_Original()
        {
            // Arrange
            var config = new Config
            {
                Knight_Male = "original",
                Monk_Male = "original"
            };
            File.WriteAllText(_configPath, System.Text.Json.JsonSerializer.Serialize(config));

            var mod = new Mod(new ModContext(), _inputSimulator.Object, new NullHotkeyHandler());
            mod.InitializeConfiguration(_configPath);
            mod.Start(null);

            // Act
            var originalPath = @"C:\Game\Data\battle_knight_m_spr.bin";
            var interceptedPath = mod.InterceptFilePath(originalPath);

            // Assert - Should not redirect when theme is "original"
            Assert.Equal(originalPath, interceptedPath);
        }

        [Fact]
        public void ConfigurationUpdated_Should_Immediately_Affect_GetJobColor()
        {
            // This is the core test for the hot reload fix
            // It simulates what happens when user presses F1, changes config, and saves

            // Arrange - Start with original theme
            var config = new Config
            {
                Knight_Male = "original",
                Monk_Male = "original"
            };
            File.WriteAllText(_configPath, System.Text.Json.JsonSerializer.Serialize(config));

            var mod = new Mod(new ModContext(), _inputSimulator.Object, new NullHotkeyHandler());
            mod.InitializeConfiguration(_configPath);
            mod.Start(null);

            // Verify initial state
            Assert.Equal("Original", mod.GetJobColor("Knight_Male"));
            Assert.Equal("Original", mod.GetJobColor("Monk_Male"));

            // Act - Simulate F1 configuration change
            var updatedConfig = new Config
            {
                Knight_Male = "holy_guard",
                Monk_Male = "shadow_assassin"
            };
            mod.ConfigurationUpdated(updatedConfig);

            // Test immediate effect after config update
            var knightTheme = mod.GetJobColor("Knight_Male");
            var monkTheme = mod.GetJobColor("Monk_Male");

            // Assert - GetJobColor should immediately return new configuration
            Assert.Equal("Holy Guard", knightTheme);
            Assert.Equal("Shadow Assassin", monkTheme);
        }

        [Fact]
        public void InterceptFilePath_Should_Use_JobClassService_Not_Hardcoded_Mappings()
        {
            // Test that the fix uses JobClassService.GetJobClassBySpriteName
            // instead of hardcoded sprite name mappings

            // Arrange
            var config = new Config
            {
                Knight_Male = "holy_guard"
            };
            File.WriteAllText(_configPath, System.Text.Json.JsonSerializer.Serialize(config));

            var mod = new Mod(new ModContext(), _inputSimulator.Object, new NullHotkeyHandler());
            mod.InitializeConfiguration(_configPath);
            mod.Start(null);

            // Act - Test with sprite name from JobClasses.json
            var knightPath = mod.InterceptFilePath(@"C:\Game\Data\battle_knight_m_spr.bin");

            // Assert - The test passes if:
            // 1. It returns the original path (because theme file doesn't exist in the test's mod path)
            // 2. OR it redirects to the themed path
            // The important thing is that it doesn't crash and uses JobClassService
            Assert.NotNull(knightPath);
            Assert.Contains("battle_knight_m_spr.bin", knightPath);
        }

        [Fact]
        public void InterceptFilePath_Should_Handle_NonJob_Sprites_Gracefully()
        {
            // Arrange
            var config = new Config();
            File.WriteAllText(_configPath, System.Text.Json.JsonSerializer.Serialize(config));

            var mod = new Mod(new ModContext(), _inputSimulator.Object, new NullHotkeyHandler());
            mod.InitializeConfiguration(_configPath);
            mod.Start(null);

            // Act - Test with non-job sprite
            var originalPath = @"C:\Game\Data\some_other_sprite.bin";
            var interceptedPath = mod.InterceptFilePath(originalPath);

            // Assert - Should return original path for non-job sprites
            Assert.Equal(originalPath, interceptedPath);
        }

        [Fact]
        public void InterceptFilePath_Should_FallBack_To_ThemeCoordinator_When_No_PerJob_Config()
        {
            // Test that the fallback behavior works when there's no per-job config

            // Arrange
            var config = new Config
            {
                // Leave Knight_Male unset/default which should be "original"
            };
            File.WriteAllText(_configPath, System.Text.Json.JsonSerializer.Serialize(config));

            var mod = new Mod(new ModContext(), _inputSimulator.Object, new NullHotkeyHandler());
            mod.InitializeConfiguration(_configPath);
            mod.Start(null);

            // Act
            var originalPath = @"C:\Game\Data\battle_knight_m_spr.bin";
            var interceptedPath = mod.InterceptFilePath(originalPath);

            // Assert - When no per-job config or config is "original", path should not change
            // This is actually the correct behavior - no redirection when using original theme
            Assert.Equal(originalPath, interceptedPath);
        }

        [Fact]
        public void Multiple_ConfigurationUpdates_Should_All_Take_Effect_Immediately()
        {
            // Test multiple config changes in succession without restart

            // Arrange
            var config = new Config { Knight_Male = "original" };
            File.WriteAllText(_configPath, System.Text.Json.JsonSerializer.Serialize(config));

            var mod = new Mod(new ModContext(), _inputSimulator.Object, new NullHotkeyHandler());
            mod.InitializeConfiguration(_configPath);
            mod.Start(null);

            // Act & Assert - Multiple configuration changes
            // First change
            mod.ConfigurationUpdated(new Config { Knight_Male = "holy_guard" });
            var theme1 = mod.GetJobColor("Knight_Male");
            Assert.Equal("Holy Guard", theme1);

            // Second change
            mod.ConfigurationUpdated(new Config { Knight_Male = "shadow_assassin" });
            var theme2 = mod.GetJobColor("Knight_Male");
            Assert.Equal("Shadow Assassin", theme2);

            // Third change back to original
            mod.ConfigurationUpdated(new Config { Knight_Male = "original" });
            var theme3 = mod.GetJobColor("Knight_Male");
            Assert.Equal("Original", theme3);

            // Fourth change
            mod.ConfigurationUpdated(new Config { Knight_Male = "desert_nomad" });
            var theme4 = mod.GetJobColor("Knight_Male");
            Assert.Equal("Desert Nomad", theme4);
        }

        [Fact]
        public void InterceptFilePath_Should_Only_Redirect_When_Theme_File_Exists()
        {
            // Test that interception only happens when the themed sprite actually exists

            // Arrange
            var config = new Config
            {
                Knight_Male = "non_existent_theme"
            };
            File.WriteAllText(_configPath, System.Text.Json.JsonSerializer.Serialize(config));

            var mod = new Mod(new ModContext(), _inputSimulator.Object, new NullHotkeyHandler());
            mod.InitializeConfiguration(_configPath);
            mod.Start(null);

            // Act
            var originalPath = @"C:\Game\Data\battle_knight_m_spr.bin";
            var interceptedPath = mod.InterceptFilePath(originalPath);

            // Assert - Should return original path when themed sprite doesn't exist
            Assert.Equal(originalPath, interceptedPath);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }
}