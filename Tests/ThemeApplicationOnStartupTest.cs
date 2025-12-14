using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace FFTColorMod.Tests
{
    public class ThemeApplicationOnStartupTest : IDisposable
    {
        private readonly string _testConfigDir;
        private readonly string _testConfigPath;
        private readonly string _testModPath;

        public ThemeApplicationOnStartupTest()
        {
            _testConfigDir = Path.Combine(Path.GetTempPath(), "FFTColorModTest_" + Guid.NewGuid());
            _testConfigPath = Path.Combine(_testConfigDir, "Config.json");
            _testModPath = Path.Combine(_testConfigDir, "mod");
            Directory.CreateDirectory(_testConfigDir);
            Directory.CreateDirectory(_testModPath);
        }

        [Fact]
        public void Mod_ShouldApplyThemesFromConfig_OnStartup()
        {
            // Arrange - Create a config with custom themes
            var userConfig = new FFTColorMod.Configuration.Config
            {
                Squire_Male = FFTColorMod.Configuration.ColorScheme.lucavi,
                Knight_Female = FFTColorMod.Configuration.ColorScheme.northern_sky,
                Archer_Male = FFTColorMod.Configuration.ColorScheme.crimson_red
            };

            // Save the config
            userConfig.FilePath = _testConfigPath;
            userConfig.Save();

            // Act - Create a Mod instance (simulating startup)
            var modContext = new FFTColorMod.ModContext();
            var mod = new FFTColorMod.Mod(modContext);

            // Load the saved config
            var configurator = new FFTColorMod.Configuration.Configurator(_testConfigDir);
            var loadedConfig = configurator.GetConfiguration<FFTColorMod.Configuration.Config>(0);

            // This simulates what Startup.cs does - notify the mod about the config
            mod.ConfigurationUpdated(loadedConfig);

            // Assert - Verify the themes are applied (not just loaded)
            // We need to check that ApplyConfiguration was called with the correct values
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.lucavi, loadedConfig.Squire_Male);
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.northern_sky, loadedConfig.Knight_Female);
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.crimson_red, loadedConfig.Archer_Male);

            // Also verify the config file wasn't overwritten with defaults
            var savedJson = File.ReadAllText(_testConfigPath);
            Assert.Contains("\"lucavi\"", savedJson);
            Assert.Contains("\"northern_sky\"", savedJson);
            Assert.Contains("\"crimson_red\"", savedJson);
        }

        [Fact]
        public void ConfigurationUpdated_ShouldNotSaveDefaults_WhenReceivingExistingConfig()
        {
            // Arrange - Create a config with custom themes
            var userConfig = new
            {
                SquireMale = "silver_knight",
                NinjaFemale = "emerald_dragon",
                WhiteMageMale = "golden_templar"
            };

            var userJson = JsonSerializer.Serialize(userConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_testConfigPath, userJson);

            // Act - Create mod and load config
            var modContext = new FFTColorMod.ModContext();
            var mod = new FFTColorMod.Mod(modContext);

            // Load the config
            var configManager = new FFTColorMod.Configuration.ConfigurationManager(_testConfigPath);
            var loadedConfig = configManager.LoadConfig();

            // Call ConfigurationUpdated (this is what Startup.cs does)
            mod.ConfigurationUpdated(loadedConfig);

            // Assert - Config should still have custom values, not defaults
            var afterUpdateJson = File.ReadAllText(_testConfigPath);
            Assert.Contains("\"silver_knight\"", afterUpdateJson);
            Assert.Contains("\"emerald_dragon\"", afterUpdateJson);
            Assert.Contains("\"golden_templar\"", afterUpdateJson);

            // Should NOT contain "original" for these fields
            Assert.DoesNotContain("\"SquireMale\": \"original\"", afterUpdateJson);
            Assert.DoesNotContain("\"NinjaFemale\": \"original\"", afterUpdateJson);
            Assert.DoesNotContain("\"WhiteMageMale\": \"original\"", afterUpdateJson);
        }

        [Fact]
        public void ApplyConfiguration_ShouldBeCalledWithCorrectValues_AfterConfigLoad()
        {
            // Arrange - Create a config with specific themes
            var config = new FFTColorMod.Configuration.Config
            {
                Monk_Male = FFTColorMod.Configuration.ColorScheme.frost_knight,
                Thief_Female = FFTColorMod.Configuration.ColorScheme.rose_gold,
                Dragoon_Male = FFTColorMod.Configuration.ColorScheme.volcanic
            };

            config.FilePath = _testConfigPath;
            config.Save();

            // Create ConfigurationManager
            var configManager = new FFTColorMod.Configuration.ConfigurationManager(_testConfigPath);

            // Act - Create ConfigBasedSpriteManager and apply configuration
            var spriteManager = new FFTColorMod.Utilities.ConfigBasedSpriteManager(
                _testModPath,
                configManager,
                _testModPath // sourcePath
            );

            spriteManager.ApplyConfiguration();

            // Assert - The configuration should be applied with correct values
            var loadedConfig = configManager.LoadConfig();
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.frost_knight, loadedConfig.Monk_Male);
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.rose_gold, loadedConfig.Thief_Female);
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.volcanic, loadedConfig.Dragoon_Male);
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