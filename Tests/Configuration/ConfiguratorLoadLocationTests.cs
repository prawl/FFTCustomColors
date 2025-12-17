using System;
using System.IO;
using System.Text.Json;
using Xunit;
using FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Tests
{
    public class ConfiguratorLoadLocationTests : IDisposable
    {
        private readonly string _testModDir;
        private readonly string _testUserDir;
        private readonly string _modConfigPath;
        private readonly string _userConfigPath;

        public ConfiguratorLoadLocationTests()
        {
            // Simulate Reloaded-II directory structure
            var testRoot = Path.Combine(Path.GetTempPath(), $"test_configurator_{Guid.NewGuid()}");

            // Mod installation directory
            _testModDir = Path.Combine(testRoot, "Mods", "FFTColorCustomizer");
            _modConfigPath = Path.Combine(_testModDir, "Config.json");

            // User configuration directory (where Reloaded-II actually stores user configs)
            _testUserDir = Path.Combine(testRoot, "User", "Mods", "ptyra.fft.colormod");
            _userConfigPath = Path.Combine(_testUserDir, "Config.json");

            Directory.CreateDirectory(_testModDir);
            Directory.CreateDirectory(_testUserDir);
        }

        public void Dispose()
        {
            var testRoot = Path.GetDirectoryName(Path.GetDirectoryName(_testModDir));
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, true);
        }

        [Fact]
        public void Configurator_ShouldLoadFromUserDirectory_NotModDirectory()
        {
            // Arrange
            // Set up different configs in each location to prove which one is loaded

            // User directory has the ACTUAL user settings (what user configured)
            var userConfig = new Config
            {
                Squire_Male = "corpse_brigade",  // corpse_brigade
                Knight_Female = "emerald_dragon" // emerald_dragon
            };
            var userJson = JsonSerializer.Serialize(userConfig, Configurable<Config>.SerializerOptions);
            File.WriteAllText(_userConfigPath, userJson);

            // Mod directory has DEFAULT settings (should NOT be used)
            var modConfig = new Config
            {
                Squire_Male = "original",  // original (default)
                Knight_Female = "original" // original (default)
            };
            var modJson = JsonSerializer.Serialize(modConfig, Configurable<Config>.SerializerOptions);
            File.WriteAllText(_modConfigPath, modJson);

            // Act
            // This is what SHOULD happen - load from User directory
            var configurator = new Configurator(_testUserDir);
            var loadedConfig = configurator.GetConfiguration<Config>(0);

            // Assert
            Assert.NotNull(loadedConfig);
            // Should have the USER configured values, not defaults
            Assert.Equal("corpse_brigade", loadedConfig.Squire_Male);   // corpse_brigade
            Assert.Equal("emerald_dragon", loadedConfig.Knight_Female); // emerald_dragon
        }

        [Fact]
        public void CurrentBug_ConfiguratorLoadsFromWrongDirectory()
        {
            // Arrange
            // User directory has what the user actually configured
            var userConfig = new Config
            {
                Squire_Male = "corpse_brigade",  // corpse_brigade
                Knight_Female = "emerald_dragon" // emerald_dragon
            };
            var userJson = JsonSerializer.Serialize(userConfig, Configurable<Config>.SerializerOptions);
            File.WriteAllText(_userConfigPath, userJson);

            // Mod directory has defaults (this is what's incorrectly being loaded)
            var modConfig = new Config
            {
                Squire_Male = "original",  // original
                Knight_Female = "original" // original
            };
            var modJson = JsonSerializer.Serialize(modConfig, Configurable<Config>.SerializerOptions);
            File.WriteAllText(_modConfigPath, modJson);

            // Act
            // This is the CURRENT BUG - loading from mod directory instead of user directory
            var configurator = new Configurator(_testModDir);  // WRONG DIRECTORY!
            var loadedConfig = configurator.GetConfiguration<Config>(0);

            // Assert - This demonstrates the bug
            Assert.NotNull(loadedConfig);
            // BUG: Gets default values instead of user configured values!
            Assert.Equal("original", loadedConfig.Squire_Male);   // original (WRONG!)
            Assert.Equal("original", loadedConfig.Knight_Female); // original (WRONG!)

            // These values SHOULD have been loaded but weren't:
            Assert.NotEqual("corpse_brigade", loadedConfig.Squire_Male);   // Should be corpse_brigade
            Assert.NotEqual("emerald_dragon", loadedConfig.Knight_Female); // Should be emerald_dragon
        }
    }
}
