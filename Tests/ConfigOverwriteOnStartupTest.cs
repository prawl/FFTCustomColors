using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace FFTColorMod.Tests
{
    public class ConfigOverwriteOnStartupTest : IDisposable
    {
        private readonly string _testConfigDir;
        private readonly string _testConfigPath;

        public ConfigOverwriteOnStartupTest()
        {
            _testConfigDir = Path.Combine(Path.GetTempPath(), "FFTColorModTest_" + Guid.NewGuid());
            _testConfigPath = Path.Combine(_testConfigDir, "Config.json");
            Directory.CreateDirectory(_testConfigDir);
        }

        [Fact]
        public void Startup_ShouldNotOverwriteUserConfig_WhenNotifyingMod()
        {
            // Arrange - Create a user config with custom themes
            var userConfig = new
            {
                SquireMale = "lucavi",
                SquireFemale = "lucavi",
                KnightMale = "corpse_brigade",
                KnightFemale = "northern_sky",
                MonkMale = "crimson_red"
            };

            var userJson = JsonSerializer.Serialize(userConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_testConfigPath, userJson);
            var originalContent = File.ReadAllText(_testConfigPath);

            // Act - Simulate what Startup.cs does
            var configurator = new FFTColorMod.Configuration.Configurator(_testConfigDir);
            var configuration = configurator.GetConfiguration<FFTColorMod.Configuration.Config>(0);

            // This is what line 62 of Startup.cs does - it should NOT trigger a save
            // _mod?.ConfigurationUpdated(_configuration);
            // We can't test the actual mod notification, but we can verify the config wasn't changed

            var afterLoadContent = File.ReadAllText(_testConfigPath);

            // Assert - Config file should not have been overwritten
            Assert.Equal(originalContent, afterLoadContent);
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.lucavi, configuration.Squire_Male);
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.lucavi, configuration.Squire_Female);
            // Note: The loaded config might have defaults for unspecified fields, but that's OK
            // The key is that the file itself shouldn't be overwritten
        }

        [Fact]
        public void ModConfigurationUpdated_ShouldNotResetToDefaults()
        {
            // Arrange - Create config with custom values
            var config = FFTColorMod.Configuration.Config.FromFile(_testConfigPath, "TestConfig");
            config.Squire_Male = FFTColorMod.Configuration.ColorScheme.lucavi;
            config.Knight_Female = FFTColorMod.Configuration.ColorScheme.northern_sky;
            config.Ninja_Male = FFTColorMod.Configuration.ColorScheme.frost_knight;
            config.Save();

            // Verify it was saved correctly
            var savedJson = File.ReadAllText(_testConfigPath);
            Assert.Contains("\"lucavi\"", savedJson);
            Assert.Contains("\"northern_sky\"", savedJson);
            Assert.Contains("\"frost_knight\"", savedJson);

            // Act - Create a Mod instance and call ConfigurationUpdated
            // This simulates what happens when Startup.cs calls _mod?.ConfigurationUpdated(_configuration)
            var modContext = new FFTColorMod.ModContext();
            var mod = new FFTColorMod.Mod(modContext);

            // Load the config fresh (like Startup does)
            var loadedConfig = FFTColorMod.Configuration.Config.FromFile(_testConfigPath, "TestConfig");

            // This is the critical call that might be resetting values
            mod.ConfigurationUpdated(loadedConfig);

            // Assert - The config values should NOT have been reset
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.lucavi, loadedConfig.Squire_Male);
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.northern_sky, loadedConfig.Knight_Female);
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.frost_knight, loadedConfig.Ninja_Male);

            // Also verify the file wasn't overwritten with defaults
            var afterUpdateJson = File.ReadAllText(_testConfigPath);
            Assert.Contains("\"lucavi\"", afterUpdateJson);
            Assert.Contains("\"northern_sky\"", afterUpdateJson);
            Assert.Contains("\"frost_knight\"", afterUpdateJson);
        }

        [Fact]
        public void ConfigurationManager_ShouldNotSaveOnLoad()
        {
            // Arrange - Create a config with non-default values
            var userConfig = new
            {
                SquireMale = "silver_knight",
                ArcherFemale = "ocean_depths",
                WhiteMageMale = "golden_templar"
            };

            var userJson = JsonSerializer.Serialize(userConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_testConfigPath, userJson);

            // Record file modification time
            var originalModTime = File.GetLastWriteTime(_testConfigPath);
            System.Threading.Thread.Sleep(100); // Ensure time difference

            // Act - Create ConfigurationManager and load config
            var configManager = new FFTColorMod.Configuration.ConfigurationManager(_testConfigPath);
            var loadedConfig = configManager.LoadConfig();

            // Assert - File should NOT have been modified (no save on load)
            var newModTime = File.GetLastWriteTime(_testConfigPath);
            Assert.Equal(originalModTime, newModTime);

            // Config values should be preserved
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.silver_knight, loadedConfig.Squire_Male);
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.ocean_depths, loadedConfig.Archer_Female);
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.golden_templar, loadedConfig.WhiteMage_Male);
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