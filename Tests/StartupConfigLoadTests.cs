using System;
using System.IO;
using System.Text.Json;
using System.Reflection;
using Xunit;
using FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Tests
{
    public class StartupConfigLoadTests : IDisposable
    {
        private readonly string _originalLocation;
        private readonly string _testRoot;
        private readonly string _testModDir;
        private readonly string _testUserDir;
        private readonly string _modConfigPath;
        private readonly string _userConfigPath;

        public StartupConfigLoadTests()
        {
            // Save original assembly location
            _originalLocation = Assembly.GetExecutingAssembly().Location;

            // Simulate Reloaded-II directory structure
            _testRoot = Path.Combine(Path.GetTempPath(), $"test_startup_{Guid.NewGuid()}");

            // Mod installation directory
            _testModDir = Path.Combine(_testRoot, "Mods", "FFTColorCustomizer");
            _modConfigPath = Path.Combine(_testModDir, "Config.json");

            // User configuration directory
            _testUserDir = Path.Combine(_testRoot, "User", "Mods", "paxtrick.fft.colorcustomizer");
            _userConfigPath = Path.Combine(_testUserDir, "Config.json");

            Directory.CreateDirectory(_testModDir);
            Directory.CreateDirectory(_testUserDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testRoot))
                Directory.Delete(_testRoot, true);
        }

        [Fact]
        public void Startup_ShouldLoadConfigFromUserDirectory()
        {
            // Arrange
            // User has configured corpse_brigade for Squire
            var userSettings = @"{
                ""SquireMale"": ""corpse_brigade"",
                ""KnightFemale"": ""emerald_dragon""
            }";
            File.WriteAllText(_userConfigPath, userSettings);

            // Mod directory has defaults (all zeros/original)
            var defaultSettings = @"{
                ""Squire_Male"": 0,
                ""Knight_Female"": 0
            }";
            File.WriteAllText(_modConfigPath, defaultSettings);

            // Act - Simulate what StartEx NOW does (CORRECT!)
            var modDirectory = _testModDir;  // This simulates getting the mod's install directory

            // Calculate the User config directory (like Startup.cs does now)
            var reloadedRoot = Path.GetDirectoryName(Path.GetDirectoryName(modDirectory));
            var userConfigDir = Path.Combine(reloadedRoot ?? "", "User", "Mods", "paxtrick.fft.colorcustomizer");

            var configurator = new Configurator(userConfigDir);  // Now using User directory!
            var config = configurator.GetConfiguration<Config>(0);

            // Assert - THIS SHOULD FAIL because we're loading from the wrong place
            Assert.NotNull(config);

            // These assertions SHOULD pass but will FAIL because of the bug
            Assert.Equal("corpse_brigade", config.Squire_Male);
            Assert.Equal("emerald_dragon", config.Knight_Female);
        }

        [Fact]
        public void ConfigDirectory_PathCalculation_IsCorrect()
        {
            // Given a mod installed at standard Reloaded location
            var modDir = @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\Reloaded\Mods\FFTColorCustomizer";

            // The user config should be at
            var expectedUserDir = @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\Reloaded\User\Mods\paxtrick.fft.colorcustomizer";

            // Calculate it the way we do in code
            var reloadedRoot = Path.GetDirectoryName(Path.GetDirectoryName(modDir));
            var actualUserDir = Path.Combine(reloadedRoot ?? "", "User", "Mods", "paxtrick.fft.colorcustomizer");

            Assert.Equal(expectedUserDir, actualUserDir);
        }
    }
}
