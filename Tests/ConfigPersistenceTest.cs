using System;
using System.IO;
using System.Text.Json;
using FFTColorMod.Configuration;
using Reloaded.Mod.Interfaces;
using Xunit;

namespace FFTColorMod.Tests
{
    public class ConfigPersistenceTest : IDisposable
    {
        private readonly string _testConfigPath;
        private readonly string _testModPath;

        public ConfigPersistenceTest()
        {
            _testModPath = Path.Combine(Path.GetTempPath(), $"test_mod_{Guid.NewGuid()}");
            _testConfigPath = Path.Combine(_testModPath, "Config.json");
            Directory.CreateDirectory(_testModPath);
        }

        [Fact]
        public void Startup_ShouldNotOverwriteExistingConfig()
        {
            // Arrange - Create a config file with custom values
            var customConfig = new Config
            {
                Squire_Male = (Configuration.ColorScheme)2,  // lucavi
                Squire_Female = (Configuration.ColorScheme)1, // corpse_brigade
                Knight_Male = (Configuration.ColorScheme)3   // northern_sky
            };

            // Save the custom config to disk using same serialization as Config uses
            var json = System.Text.Json.JsonSerializer.Serialize(customConfig,
                Configuration.Configurable<Config>.SerializerOptions);
            File.WriteAllText(_testConfigPath, json);

            // Act - Simulate what happens during startup
            var configurator = new Configurator(_testModPath);
            var loadedConfig = configurator.GetConfiguration<Config>(0);

            // The issue: OnConfigurationUpdated is called which saves the config
            // This simulates what Startup.cs does
            if (loadedConfig is IConfigurable configurable)
            {
                configurable.Save.Invoke();
            }

            // Reload the config from disk to see what was saved
            var savedJson = File.ReadAllText(_testConfigPath);
            var reloadedConfig = System.Text.Json.JsonSerializer.Deserialize<Config>(savedJson,
                Configuration.Configurable<Config>.SerializerOptions);

            // Assert - The custom values should still be there, not overwritten with defaults
            Assert.Equal((Configuration.ColorScheme)2, reloadedConfig.Squire_Male);  // lucavi
            Assert.Equal((Configuration.ColorScheme)1, reloadedConfig.Squire_Female); // corpse_brigade
            Assert.Equal((Configuration.ColorScheme)3, reloadedConfig.Knight_Male);   // northern_sky
        }

        [Fact]
        public void ConfigurationManager_ShouldPreserveExistingValues()
        {
            // Arrange - Create a config with custom values
            var manager = new ConfigurationManager(_testConfigPath);
            var customConfig = new Config
            {
                Squire_Male = (Configuration.ColorScheme)2,  // lucavi
                Knight_Male = (Configuration.ColorScheme)1   // corpse_brigade
            };
            manager.SaveConfig(customConfig);

            // Act - Create a new manager and load the config
            var newManager = new ConfigurationManager(_testConfigPath);
            var loadedConfig = newManager.LoadConfig();

            // Assert - Values should be preserved
            Assert.Equal((Configuration.ColorScheme)2, loadedConfig.Squire_Male);  // lucavi
            Assert.Equal((Configuration.ColorScheme)1, loadedConfig.Knight_Male);  // corpse_brigade
        }

        [Fact]
        public void Configurator_ShouldLoadExistingConfigNotCreateNew()
        {
            // Arrange - Create existing config with custom values
            var customConfig = new Config
            {
                Squire_Male = (Configuration.ColorScheme)2,    // lucavi
                Archer_Female = (Configuration.ColorScheme)3   // northern_sky
            };
            var json = System.Text.Json.JsonSerializer.Serialize(customConfig,
                Configuration.Configurable<Config>.SerializerOptions);
            File.WriteAllText(_testConfigPath, json);

            // Act - Create configurator which should load existing config
            var configurator = new Configurator(_testModPath);
            var config = configurator.GetConfiguration<Config>(0);

            // Assert - Should have loaded the existing values, not defaults
            Assert.Equal((Configuration.ColorScheme)2, config.Squire_Male);    // lucavi
            Assert.Equal((Configuration.ColorScheme)3, config.Archer_Female);  // northern_sky

            // Other values should be default
            Assert.Equal((Configuration.ColorScheme)0, config.Knight_Male);    // original
        }

        public void Dispose()
        {
            if (Directory.Exists(_testModPath))
            {
                Directory.Delete(_testModPath, true);
            }
        }
    }
}