using System;
using System.IO;
using Xunit;
using FFTColorMod;
using FFTColorMod.Configuration;
using Reloaded.Mod.Interfaces;

namespace FFTColorMod.Tests
{
    public class ConfigurableTests : IDisposable
    {
        private readonly string _testConfigPath;
        private readonly string _testModPath;

        public ConfigurableTests()
        {
            _testModPath = Path.Combine(Path.GetTempPath(), $"test_mod_{Guid.NewGuid()}");
            _testConfigPath = Path.Combine(_testModPath, "Config.json");
            Directory.CreateDirectory(_testModPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testModPath))
            {
                try
                {
                    Directory.Delete(_testModPath, true);
                }
                catch { }
            }
        }

        [Fact]
        public void Configurator_ShouldCreateDefaultConfig()
        {
            // Arrange
            var configurator = new Configurator(_testModPath);

            // Act
            var config = configurator.GetConfiguration<Config>(0);

            // Assert
            Assert.NotNull(config);
            Assert.Equal("original", config.KnightMale);
            Assert.Equal("original", config.ArcherFemale);
        }

        [Fact]
        public void Configurator_ShouldSaveConfig()
        {
            // Arrange
            var configurator = new Configurator(_testModPath);
            var config = new Config
            {
                KnightMale = "corpse_brigade",
                ArcherFemale = "lucavi"
            };

            // Act
            configurator.SetConfiguration(0, config);

            // Assert
            var savedConfig = configurator.GetConfiguration<Config>(0);
            Assert.Equal("corpse_brigade", savedConfig.KnightMale);
            Assert.Equal("lucavi", savedConfig.ArcherFemale);
        }

        [Fact]
        public void Configurator_ShouldMigrateConfigs()
        {
            // Arrange
            var configurator = new Configurator(_testModPath);
            var oldConfig = new Config
            {
                KnightMale = "smoke"
            };
            configurator.SetConfiguration(0, oldConfig);

            // Act
            configurator.Migrate(oldConfig, oldConfig);

            // Assert
            var migratedConfig = configurator.GetConfiguration<Config>(0);
            Assert.Equal("smoke", migratedConfig.KnightMale);
        }

        [Fact]
        public void Configurator_ShouldHandleConfigEvents()
        {
            // Arrange
            var configurator = new Configurator(_testModPath);
            var eventFired = false;
            configurator.ConfigurationUpdated += (config) =>
            {
                eventFired = true;
            };

            var config = new Config
            {
                MonkMale = "northern_sky"
            };

            // Act
            configurator.SetConfiguration(0, config);

            // Assert
            Assert.True(eventFired);
        }

        [Fact]
        public void Mod_ShouldImplementIConfigurable()
        {
            // Arrange & Act
            var mod = new Mod();

            // Assert
            Assert.True(mod is IConfigurable);
        }

        [Fact]
        public void Mod_ShouldUpdateColorsOnConfigChange()
        {
            // Arrange
            var mod = new Mod();
            var config = new Config
            {
                KnightMale = "lucavi",
                DragoonFemale = "corpse_brigade"
            };

            // Act
            mod.ConfigurationUpdated(config);

            // Assert
            Assert.Equal("lucavi", mod.GetJobColor("KnightMale"));
            Assert.Equal("corpse_brigade", mod.GetJobColor("DragoonFemale"));
        }

        [Fact]
        public void Mod_ShouldInitializeReloadedConfigManager()
        {
            // Arrange
            var testPath = Path.Combine(_testModPath, "testconfig.json");
            var mod = new Mod();
            var config = new Config
            {
                ThiefMale = "northern_sky",
                WhiteMageFemale = "southern_sky"
            };

            // Act
            mod.InitializeConfiguration(testPath);
            mod.ConfigurationUpdated(config);

            // Assert
            Assert.Equal("northern_sky", mod.GetJobColor("ThiefMale"));
            Assert.Equal("southern_sky", mod.GetJobColor("WhiteMageFemale"));
        }

        [Fact]
        public void Mod_ShouldPersistConfigurationChanges()
        {
            // Arrange
            var testPath = Path.Combine(_testModPath, "testconfig.json");
            var mod = new Mod();
            var config = new Config
            {
                KnightFemale = "smoke",
                ArcherMale = "corpse_brigade"
            };

            // Act
            mod.InitializeConfiguration(testPath);
            mod.ConfigurationUpdated(config);

            // Create a new Mod instance to verify persistence
            var mod2 = new Mod();
            mod2.InitializeConfiguration(testPath);

            // Assert
            Assert.Equal("smoke", mod2.GetJobColor("KnightFemale"));
            Assert.Equal("corpse_brigade", mod2.GetJobColor("ArcherMale"));
        }

        [Fact]
        public void Configurator_ShouldTriggerEventWhenConfigurationChanges()
        {
            // Arrange
            var configurator = new Configurator(_testModPath);
            Config? receivedConfig = null;
            configurator.ConfigurationUpdated += (config) =>
            {
                receivedConfig = config;
            };

            var newConfig = new Config
            {
                NinjaMale = "lucavi",
                DancerFemale = "northern_sky"
            };

            // Act
            configurator.SetConfiguration(0, newConfig);

            // Assert
            Assert.NotNull(receivedConfig);
            Assert.Equal("lucavi", receivedConfig.NinjaMale);
            Assert.Equal("northern_sky", receivedConfig.DancerFemale);
        }

        [Fact]
        public void Mod_Save_ShouldPersistCurrentConfiguration()
        {
            // Arrange
            var testPath = Path.Combine(_testModPath, "savetest.json");
            var mod = new Mod();
            mod.InitializeConfiguration(testPath);

            var config = new Config
            {
                SamuraiMale = "smoke",
                GeomancerFemale = "lucavi"
            };
            mod.ConfigurationUpdated(config);

            // Act
            mod.Save(); // This should save the current configuration

            // Assert - Load config from file to verify it was saved
            var mod2 = new Mod();
            mod2.InitializeConfiguration(testPath);
            Assert.Equal("smoke", mod2.GetJobColor("SamuraiMale"));
            Assert.Equal("lucavi", mod2.GetJobColor("GeomancerFemale"));
        }

        [Fact]
        public void Mod_GetAllJobColors_ShouldReturnCurrentConfiguration()
        {
            // Arrange
            var mod = new Mod();
            var config = new Config
            {
                KnightMale = "northern_sky",
                ArcherFemale = "smoke",
                MonkMale = "lucavi"
            };

            // Act
            mod.ConfigurationUpdated(config);
            var allColors = mod.GetAllJobColors();

            // Assert
            Assert.NotNull(allColors);
            Assert.Contains("KnightMale", allColors.Keys);
            Assert.Contains("ArcherFemale", allColors.Keys);
            Assert.Contains("MonkMale", allColors.Keys);
            Assert.Equal("northern_sky", allColors["KnightMale"]);
            Assert.Equal("smoke", allColors["ArcherFemale"]);
            Assert.Equal("lucavi", allColors["MonkMale"]);
        }

        [Fact]
        public void Mod_ShouldApplyConfigurationWhenUpdated()
        {
            // Arrange
            var testPath = Path.Combine(_testModPath, "applytest.json");
            var mod = new Mod();
            mod.InitializeConfiguration(testPath);

            var initialConfig = new Config
            {
                WhiteMageMale = "original",
                BlackMageFemale = "original"
            };
            mod.ConfigurationUpdated(initialConfig);

            // Act - Update configuration
            var newConfig = new Config
            {
                WhiteMageMale = "corpse_brigade",
                BlackMageFemale = "southern_sky"
            };
            mod.ConfigurationUpdated(newConfig);

            // Assert - Verify the configuration is applied
            Assert.Equal("corpse_brigade", mod.GetJobColor("WhiteMageMale"));
            Assert.Equal("southern_sky", mod.GetJobColor("BlackMageFemale"));

            // Verify it's actually persisted
            var mod2 = new Mod();
            mod2.InitializeConfiguration(testPath);
            Assert.Equal("corpse_brigade", mod2.GetJobColor("WhiteMageMale"));
            Assert.Equal("southern_sky", mod2.GetJobColor("BlackMageFemale"));
        }
    }
}