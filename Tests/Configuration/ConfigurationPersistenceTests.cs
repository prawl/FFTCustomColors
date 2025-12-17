using System;
using System.IO;
using Xunit;
using FFTColorCustomizer.Configuration;
using System.Text.Json;

namespace FFTColorCustomizer.Tests
{
    public class ConfigurationPersistenceTests : IDisposable
    {
        private readonly string _testConfigPath;
        private readonly string _testConfigDir;

        public ConfigurationPersistenceTests()
        {
            _testConfigDir = Path.Combine(Path.GetTempPath(), $"test_config_dir_{Guid.NewGuid()}");
            _testConfigPath = Path.Combine(_testConfigDir, "Config.json");
            Directory.CreateDirectory(_testConfigDir);
        }

        public void Dispose()
        {
            // Add a small delay to ensure file handles are released
            System.Threading.Thread.Sleep(50);

            if (Directory.Exists(_testConfigDir))
            {
                try
                {
                    Directory.Delete(_testConfigDir, true);
                }
                catch (IOException)
                {
                    // Wait a bit more and try again
                    System.Threading.Thread.Sleep(100);
                    try
                    {
                        Directory.Delete(_testConfigDir, true);
                    }
                    catch
                    {
                        // Ignore cleanup errors - the temp directory will be cleaned up eventually
                    }
                }
            }
        }

        [Fact]
        public void ConfiguratorMixin_SavesConfigurationToDisk_WhenSaveIsCalled()
        {
            // Arrange
            var configuratorMixin = new ConfiguratorMixin();
            var configurations = configuratorMixin.MakeConfigurations(_testConfigDir);
            var config = configurations[0] as Config;

            Assert.NotNull(config);

            // Act - Update the configuration
            config.Squire_Male = "ocean_depths";  // crimson_red
            config.Knight_Male = "rose_gold";  // rose_gold
            config.Knight_Female = "emerald_dragon";  // emerald_dragon

            // Save the configuration
            config.Save();

            // Assert - Verify the file was saved with correct values
            Assert.True(File.Exists(_testConfigPath), "Config.json should be created");

            var savedJson = File.ReadAllText(_testConfigPath);
            var savedConfig = JsonSerializer.Deserialize<Config>(savedJson, new JsonSerializerOptions
            {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            });

            Assert.NotNull(savedConfig);
            Assert.Equal("ocean_depths", savedConfig.Squire_Male);  // crimson_red
            Assert.Equal("rose_gold", savedConfig.Knight_Male);  // rose_gold
            Assert.Equal("emerald_dragon", savedConfig.Knight_Female);  // emerald_dragon
        }

        [Fact]
        public void ConfiguratorMixin_LoadsExistingConfiguration_WhenFileExists()
        {
            // Arrange - Create a config file with specific values
            var existingConfig = new Config
            {
                Squire_Male = "corpse_brigade",  // corpse_brigade
                Dragoon_Female = "lucavi"  // lucavi
            };

            Directory.CreateDirectory(_testConfigDir);
            var json = JsonSerializer.Serialize(existingConfig, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            });
            File.WriteAllText(_testConfigPath, json);

            // Act
            var configuratorMixin = new ConfiguratorMixin();
            var configurations = configuratorMixin.MakeConfigurations(_testConfigDir);
            var loadedConfig = configurations[0] as Config;

            // Assert
            Assert.NotNull(loadedConfig);
            Assert.Equal("corpse_brigade", loadedConfig.Squire_Male);  // corpse_brigade
            Assert.Equal("lucavi", loadedConfig.Dragoon_Female);  // lucavi
            Assert.Equal("original", loadedConfig.Knight_Male); // original - default
        }

        [Fact]
        public void Configurator_SaveMethod_PersistsAllConfigurationsToDisk()
        {
            // Arrange
            var configurator = new Configurator(_testConfigDir);
            var config = configurator.GetConfiguration<Config>(0);

            // Act - Modify configuration
            config.Squire_Male = "blood_moon";  // blood_moon
            config.Archer_Female = "celestial";  // celestial

            // Save through the Configurator
            configurator.Save();

            // Assert - Verify persistence
            var savedJson = File.ReadAllText(_testConfigPath);
            var savedConfig = JsonSerializer.Deserialize<Config>(savedJson, new JsonSerializerOptions
            {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            });

            Assert.NotNull(savedConfig);
            Assert.Equal("blood_moon", savedConfig.Squire_Male);  // blood_moon
            Assert.Equal("celestial", savedConfig.Archer_Female);  // celestial
        }
    }
}
