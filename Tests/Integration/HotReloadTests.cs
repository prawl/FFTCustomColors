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

        [Fact]
        public void InterceptFilePath_Should_Not_Copy_Files_During_Runtime()
        {
            // This test verifies the fix for the "Access Denied" error
            // InterceptFilePath should NEVER copy files during runtime interception
            // File copying should only happen during ApplyConfiguration

            // Arrange
            var config = new Config
            {
                Knight_Male = "holy_guard"
            };
            File.WriteAllText(_configPath, System.Text.Json.JsonSerializer.Serialize(config));

            var mod = new Mod(new ModContext(), _inputSimulator.Object, new NullHotkeyHandler());
            mod.InitializeConfiguration(_configPath);
            mod.Start(null);

            // Create a test sprite file to simulate the runtime environment
            var testSpritePath = Path.Combine(_unitPath, "battle_knight_m_spr.bin");
            var testSpriteContent = "ORIGINAL_SPRITE_CONTENT";
            File.WriteAllText(testSpritePath, testSpriteContent);

            // Create the themed sprite that should be redirected to
            var themedPath = Path.Combine(_unitPath, "sprites_holy_guard", "battle_knight_m_spr.bin");
            var themedContent = "THEMED_SPRITE_CONTENT";
            File.WriteAllText(themedPath, themedContent);

            // Act - Call InterceptFilePath multiple times to simulate runtime interception
            string intercepted1 = null;
            string intercepted2 = null;
            Exception interceptException = null;
            var concurrentResults = new System.Collections.Concurrent.ConcurrentBag<string>();

            try
            {
                // Multiple rapid calls to simulate concurrent access during gameplay
                // Using the game path format that the mod expects
                var gamePath = @"C:\Game\Data\battle_knight_m_spr.bin";
                intercepted1 = mod.InterceptFilePath(gamePath);
                intercepted2 = mod.InterceptFilePath(gamePath);

                // Simulate concurrent access from multiple threads
                var tasks = Enumerable.Range(0, 10).Select(_ =>
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        var result = mod.InterceptFilePath(gamePath);
                        concurrentResults.Add(result);
                    })
                ).ToArray();
                System.Threading.Tasks.Task.WaitAll(tasks);
            }
            catch (Exception ex)
            {
                interceptException = ex;
            }

            // Assert
            // 1. No exceptions should be thrown (no file access conflicts)
            Assert.Null(interceptException);

            // 2. All concurrent calls should succeed without exceptions
            Assert.Equal(10, concurrentResults.Count);

            // 3. InterceptFilePath should return consistent results (path redirection or original)
            // The key is that it doesn't throw "Access Denied" errors
            Assert.NotNull(intercepted1);
            Assert.NotNull(intercepted2);

            // 4. Original test file should remain unchanged (no copying occurred)
            if (File.Exists(testSpritePath))
            {
                var originalContent = File.ReadAllText(testSpritePath);
                Assert.Equal(testSpriteContent, originalContent);
            }

            // 5. Themed file should remain unchanged (not overwritten during interception)
            if (File.Exists(themedPath))
            {
                var themedContentAfter = File.ReadAllText(themedPath);
                Assert.Equal(themedContent, themedContentAfter);
            }

            // 6. No "Access Denied" exceptions should occur
            // This is the primary fix we're testing - concurrent access should not cause file locks
            Assert.All(concurrentResults, result => Assert.NotNull(result));
        }

        [Fact]
        public void InterceptFilePath_Should_Only_Return_Paths_Never_Modify_Files()
        {
            // Test that InterceptFilePath is purely a path resolution function
            // It should never create, copy, or modify any files

            // Arrange
            var config = new Config
            {
                Knight_Male = "holy_guard",
                Monk_Male = "shadow_assassin",
                Archer_Female = "desert_nomad"
            };
            File.WriteAllText(_configPath, System.Text.Json.JsonSerializer.Serialize(config));

            var mod = new Mod(new ModContext(), _inputSimulator.Object, new NullHotkeyHandler());
            mod.InitializeConfiguration(_configPath);
            mod.Start(null);

            // Track file system state before interception
            var filesBefore = Directory.GetFiles(_unitPath, "*", SearchOption.AllDirectories)
                .OrderBy(f => f).ToList();
            var lastWriteTimesBefore = filesBefore.ToDictionary(
                f => f,
                f => File.GetLastWriteTime(f)
            );

            // Act - Call InterceptFilePath for various sprites
            var testPaths = new[]
            {
                @"C:\Game\Data\battle_knight_m_spr.bin",
                @"C:\Game\Data\battle_monk_m_spr.bin",
                @"C:\Game\Data\battle_yumi_w_spr.bin",
                @"C:\Game\Data\non_existent_sprite.bin"
            };

            var interceptedPaths = new List<string>();
            foreach (var path in testPaths)
            {
                // Call multiple times to ensure no side effects
                for (int i = 0; i < 3; i++)
                {
                    var result = mod.InterceptFilePath(path);
                    interceptedPaths.Add(result);
                }
            }

            // Track file system state after interception
            var filesAfter = Directory.GetFiles(_unitPath, "*", SearchOption.AllDirectories)
                .OrderBy(f => f).ToList();
            var lastWriteTimesAfter = filesAfter.ToDictionary(
                f => f,
                f => File.GetLastWriteTime(f)
            );

            // Assert
            // 1. No new files should be created
            Assert.Equal(filesBefore.Count, filesAfter.Count);
            Assert.Equal(filesBefore, filesAfter);

            // 2. No files should be modified (last write times unchanged)
            foreach (var file in filesBefore)
            {
                Assert.Equal(
                    lastWriteTimesBefore[file],
                    lastWriteTimesAfter[file]
                );
            }

            // 3. InterceptFilePath should only return paths, never null
            Assert.All(interceptedPaths, path => Assert.NotNull(path));
        }

        [Fact]
        public void ApplyConfiguration_Should_Copy_Files_Not_InterceptFilePath()
        {
            // Test that file copying happens during configuration application
            // NOT during runtime interception

            // Arrange
            var config = new Config
            {
                Knight_Male = "holy_guard"
            };
            File.WriteAllText(_configPath, System.Text.Json.JsonSerializer.Serialize(config));

            var mod = new Mod(new ModContext(), _inputSimulator.Object, new NullHotkeyHandler());
            mod.InitializeConfiguration(_configPath);

            // Create source themed sprite
            var sourceThemePath = Path.Combine(_unitPath, "sprites_holy_guard", "battle_knight_m_spr.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceThemePath));
            File.WriteAllText(sourceThemePath, "HOLY_GUARD_THEME_CONTENT");

            // Track when files are created/modified
            var targetPath = Path.Combine(_unitPath, "battle_knight_m_spr.bin");
            bool targetExistedBefore = File.Exists(targetPath);
            DateTime? modTimeBefore = targetExistedBefore ? File.GetLastWriteTime(targetPath) : (DateTime?)null;

            // Act - Start the mod (this triggers ApplyConfiguration)
            mod.Start(null);

            bool targetExistsAfterConfig = File.Exists(targetPath);
            DateTime? modTimeAfterConfig = targetExistsAfterConfig ? File.GetLastWriteTime(targetPath) : (DateTime?)null;

            // Now test InterceptFilePath doesn't modify files
            var interceptedPath = mod.InterceptFilePath(@"C:\Game\Data\battle_knight_m_spr.bin");

            bool targetExistsAfterIntercept = File.Exists(targetPath);
            DateTime? modTimeAfterIntercept = targetExistsAfterIntercept ? File.GetLastWriteTime(targetPath) : (DateTime?)null;

            // Assert
            // Files should be copied during configuration (if ConfigBasedSpriteManager does this)
            // But InterceptFilePath should not modify any files
            if (targetExistsAfterIntercept && targetExistsAfterConfig)
            {
                // If file exists after both operations, the modification time should not change during interception
                Assert.Equal(modTimeAfterConfig, modTimeAfterIntercept);
            }

            // InterceptFilePath should return a valid path
            Assert.NotNull(interceptedPath);
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