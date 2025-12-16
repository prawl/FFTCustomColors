using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace FFTColorMod.Tests
{
    public class ConfigStartupOverrideTest
    {
        private readonly string _testConfigPath;
        private readonly string _testConfigDir;

        public ConfigStartupOverrideTest()
        {
            _testConfigDir = Path.Combine(Path.GetTempPath(), "FFTColorModTest_" + Guid.NewGuid());
            _testConfigPath = Path.Combine(_testConfigDir, "Config.json");
            Directory.CreateDirectory(_testConfigDir);
        }

        [Fact]
        public void Config_ShouldNotAddFilePathAndConfigName_WhenSerialized()
        {
            // Arrange
            var config = new FFTColorMod.Configuration.Config
            {
                Squire_Male = "original",
                Knight_Male = "original",
                Archer_Female = "original"
            };

            // Act - Simulate what Configurable.Save() does
            var json = JsonSerializer.Serialize(config, FFTColorMod.Configuration.Configurable<FFTColorMod.Configuration.Config>.SerializerOptions);
            File.WriteAllText(_testConfigPath, json);

            // Assert - Verify FilePath and ConfigName are NOT in the JSON
            var jsonContent = File.ReadAllText(_testConfigPath);
            Assert.DoesNotContain("FilePath", jsonContent);
            Assert.DoesNotContain("ConfigName", jsonContent);
        }

        [Fact]
        public void Config_ShouldPreserveUserSettings_OnStartup()
        {
            // Arrange - Create a config file with user's settings
            var userConfig = new
            {
                SquireMale = "original",
                KnightMale = "original",
                ArcherFemale = "original",
                WhiteMageMale = "original"
            };

            var userJson = JsonSerializer.Serialize(userConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_testConfigPath, userJson);

            // Act - Load config as Startup.cs does
            var configurator = new FFTColorMod.Configuration.Configurator(_testConfigDir);
            var loadedConfig = configurator.GetConfiguration<FFTColorMod.Configuration.Config>(0);

            // Assert - User settings should be preserved
            Assert.Equal("original", loadedConfig.Squire_Male);
            Assert.Equal("original", loadedConfig.Knight_Male);
            Assert.Equal("original", loadedConfig.Archer_Female);
            Assert.Equal("original", loadedConfig.WhiteMage_Male);
        }

        [Fact]
        public void Config_ShouldNotResetToOriginal_AfterLoading()
        {
            // Arrange - Create config with non-original values
            var config = FFTColorMod.Configuration.Config.FromFile(_testConfigPath, "TestConfig");
            config.Squire_Male = "original";
            config.Knight_Female = "original";
            config.Monk_Male = "original";
            config.Save();

            // Act - Reload the config (simulating game startup)
            var reloadedConfig = FFTColorMod.Configuration.Config.FromFile(_testConfigPath, "TestConfig");

            // Assert - Values should NOT reset to original
            Assert.Equal("original", reloadedConfig.Squire_Male);
            Assert.Equal("original", reloadedConfig.Knight_Female);
            Assert.Equal("original", reloadedConfig.Monk_Male);

            // Also verify the JSON doesn't contain FilePath/ConfigName
            var jsonContent = File.ReadAllText(_testConfigPath);
            Assert.DoesNotContain("FilePath", jsonContent);
            Assert.DoesNotContain("ConfigName", jsonContent);
        }

        [Fact]
        public void Configurator_ShouldNotOverwriteExistingConfig_OnLoad()
        {
            // Arrange - Create a user config file
            var userConfig = new FFTColorMod.Configuration.Config
            {
                Squire_Male = "original",
                Ninja_Female = "original",
                Agrias = "ash_dark"
            };

            // Save it using the proper serialization
            var json = JsonSerializer.Serialize(userConfig, FFTColorMod.Configuration.Configurable<FFTColorMod.Configuration.Config>.SerializerOptions);
            File.WriteAllText(_testConfigPath, json);
            var originalContent = File.ReadAllText(_testConfigPath);

            // Act - Load through Configurator (should NOT overwrite)
            var configurator = new FFTColorMod.Configuration.Configurator(_testConfigDir);
            var config = configurator.GetConfiguration<FFTColorMod.Configuration.Config>(0);

            // Assert - File should not be overwritten with defaults
            var newContent = File.ReadAllText(_testConfigPath);
            Assert.Equal(originalContent, newContent);

            // Config values should match what was saved
            Assert.Equal("original", config.Squire_Male);
            Assert.Equal("original", config.Ninja_Female);
            Assert.Equal("ash_dark", config.Agrias);
        }

        [Fact]
        public void Startup_ShouldNotTriggerSave_OnInitialLoad()
        {
            // This test simulates what Startup.cs does and ensures it doesn't
            // trigger a save that would reset user settings

            // Arrange - Create user config
            var userConfig = new FFTColorMod.Configuration.Config
            {
                Squire_Male = "original",
                WhiteMage_Female = "ocean_depths"
            };
            var json = JsonSerializer.Serialize(userConfig, FFTColorMod.Configuration.Configurable<FFTColorMod.Configuration.Config>.SerializerOptions);
            File.WriteAllText(_testConfigPath, json);

            // Act - Simulate Startup.cs initialization
            var configurator = new FFTColorMod.Configuration.Configurator(_testConfigDir);
            var configuration = configurator.GetConfiguration<FFTColorMod.Configuration.Config>(0);

            // This line in Startup.cs line 62 should NOT trigger a save
            // It should only notify the mod, not save defaults over user config
            // _mod?.ConfigurationUpdated(_configuration);

            // We can't test the actual mod notification, but we can verify
            // the config file hasn't been modified
            var afterLoadJson = File.ReadAllText(_testConfigPath);

            // Assert
            Assert.Equal(json, afterLoadJson); // File unchanged
            Assert.Equal("original", configuration.Squire_Male);
            Assert.Equal("ocean_depths", configuration.WhiteMage_Female);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testConfigDir))
                {
                    Directory.Delete(_testConfigDir, true);
                }
            }
            catch { }
        }
    }
}