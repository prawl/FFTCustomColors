using System;
using System.IO;
using System.Text.Json;
using FFTColorCustomizer.Configuration;
using Reloaded.Mod.Interfaces;
using Xunit;

namespace FFTColorCustomizer.Tests
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
                Squire_Male = "lucavi",  // lucavi
                Squire_Female = "corpse_brigade", // corpse_brigade
                Knight_Male = "northern_sky"   // northern_sky
            };

            // Save the custom config to disk using same serialization as Config uses
            var json = System.Text.Json.JsonSerializer.Serialize(customConfig,
                Configurable<Config>.SerializerOptions);
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
                Configurable<Config>.SerializerOptions);

            // Assert - The custom values should still be there, not overwritten with defaults
            Assert.Equal("lucavi", reloadedConfig.Squire_Male);  // lucavi
            Assert.Equal("corpse_brigade", reloadedConfig.Squire_Female); // corpse_brigade
            Assert.Equal("northern_sky", reloadedConfig.Knight_Male);   // northern_sky
        }

        [Fact]
        public void ConfigurationManager_ShouldPreserveExistingValues()
        {
            // Arrange - Create a config with custom values
            var manager = new ConfigurationManager(_testConfigPath);
            var customConfig = new Config
            {
                Squire_Male = "lucavi",  // lucavi
                Knight_Male = "corpse_brigade"   // corpse_brigade
            };
            manager.SaveConfig(customConfig);

            // Act - Create a new manager and load the config
            var newManager = new ConfigurationManager(_testConfigPath);
            var loadedConfig = newManager.LoadConfig();

            // Assert - Values should be preserved
            Assert.Equal("lucavi", loadedConfig.Squire_Male);  // lucavi
            Assert.Equal("corpse_brigade", loadedConfig.Knight_Male);  // corpse_brigade
        }

        [Fact]
        public void Configurator_ShouldLoadExistingConfigNotCreateNew()
        {
            // Arrange - Create existing config with custom values
            var customConfig = new Config
            {
                Squire_Male = "lucavi",    // lucavi
                Archer_Female = "northern_sky"   // northern_sky
            };
            var json = System.Text.Json.JsonSerializer.Serialize(customConfig,
                Configurable<Config>.SerializerOptions);
            File.WriteAllText(_testConfigPath, json);

            // Act - Create configurator which should load existing config
            var configurator = new Configurator(_testModPath);
            var config = configurator.GetConfiguration<Config>(0);

            // Assert - Should have loaded the existing values, not defaults
            Assert.Equal("lucavi", config.Squire_Male);    // lucavi
            Assert.Equal("northern_sky", config.Archer_Female);  // northern_sky

            // Other values should be default
            Assert.Equal("original", config.Knight_Male);    // original
        }

        public void Dispose()
        {
            // Add a small delay to ensure file handles are released
            System.Threading.Thread.Sleep(100);

            // Force garbage collection to ensure any finalizers run
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            if (Directory.Exists(_testModPath))
            {
                try
                {
                    // Try to delete all files first with retry logic
                    foreach (var file in Directory.GetFiles(_testModPath, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            File.SetAttributes(file, FileAttributes.Normal);
                            File.Delete(file);
                        }
                        catch
                        {
                            // If we can't delete a file, try again after a short delay
                            System.Threading.Thread.Sleep(50);
                            try
                            {
                                File.Delete(file);
                            }
                            catch
                            {
                                // Ignore - best effort cleanup
                            }
                        }
                    }

                    // Now try to delete the directory
                    Directory.Delete(_testModPath, true);
                }
                catch (IOException)
                {
                    // Ignore cleanup errors in tests - this is best effort
                    // The temp directory will be cleaned up eventually by the OS
                }
            }
        }
    }
}
