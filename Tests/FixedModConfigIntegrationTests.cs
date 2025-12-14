using System;
using System.IO;
using Xunit;
using FFTColorMod;
using FFTColorMod.Configuration;
using FFTColorMod.Utilities;

namespace FFTColorMod.Tests
{
    public class FixedModConfigIntegrationTests : IDisposable
    {
        private readonly string _testModPath;
        private readonly string _testConfigPath;
        private readonly ModContext _modContext;
        private readonly TestInputSimulator _inputSimulator;

        public FixedModConfigIntegrationTests()
        {
            _testModPath = Path.Combine(Path.GetTempPath(), $"test_mod_{Guid.NewGuid()}");
            _testConfigPath = Path.Combine(_testModPath, "Config.json");

            // Create test directory structure
            CreateTestDirectoryStructure();

            _modContext = new ModContext();
            _inputSimulator = new TestInputSimulator();
        }

        private void CreateTestDirectoryStructure()
        {
            var unitDir = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(unitDir);

            // Create color scheme directories - include all schemes used in tests
            var schemes = new[] { "sprites_original", "sprites_corpse_brigade", "sprites_lucavi", "sprites_northern_sky" };
            foreach (var scheme in schemes)
            {
                var schemeDir = Path.Combine(unitDir, scheme);
                Directory.CreateDirectory(schemeDir);

                // Create dummy sprite files for all jobs used in tests
                File.WriteAllText(Path.Combine(schemeDir, "battle_knight_m_spr.bin"), $"{scheme}_knight");
                File.WriteAllText(Path.Combine(schemeDir, "battle_yumi_w_spr.bin"), $"{scheme}_archer");
                File.WriteAllText(Path.Combine(schemeDir, "battle_monk_m_spr.bin"), $"{scheme}_monk");
            }
        }

        public void Dispose()
        {
            // Add a small delay to ensure file handles are released
            System.Threading.Thread.Sleep(100);

            if (Directory.Exists(_testModPath))
            {
                try
                {
                    Directory.Delete(_testModPath, true);
                }
                catch (IOException)
                {
                    // Ignore cleanup errors in tests
                }
            }
        }

        [Fact]
        public void SetJobColor_WithProperInitialization_ShouldWork()
        {
            // Arrange
            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            var mod = new Mod(_modContext, _inputSimulator);

            // IMPORTANT: Call InitializeConfiguration to set up the managers
            mod.InitializeConfiguration(_testConfigPath);

            // Act
            mod.SetJobColor("Archer_Female", "lucavi");

            // Assert
            var color = mod.GetJobColor("Archer_Female");
            Assert.Equal("Lucavi", color);

            // Verify the config was saved
            Assert.True(File.Exists(_testConfigPath));

            // Load config directly and verify
            var configManager = new ConfigurationManager(_testConfigPath);
            var config = configManager.LoadConfig();
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)2, config.Archer_Female); // lucavi
        }

        [Fact]
        public void SetJobColor_MultipleProperties_ShouldSetCorrectly()
        {
            // Arrange
            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            var mod = new Mod(_modContext, _inputSimulator);
            mod.InitializeConfiguration(_testConfigPath);

            // Act
            mod.SetJobColor("Knight_Male", "corpse_brigade");
            mod.SetJobColor("Archer_Female", "lucavi");
            mod.SetJobColor("Monk_Male", "northern_sky");

            // Verify the config file was actually written
            Assert.True(File.Exists(_testConfigPath), "Config file should exist after setting colors");

            // Load config directly from disk using a fresh ConfigurationManager
            var freshConfigManager = new ConfigurationManager(_testConfigPath);
            var diskConfig = freshConfigManager.LoadConfig();

            // Assert - verify the values were persisted to disk
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)1, diskConfig.Knight_Male); // corpse_brigade
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)2, diskConfig.Archer_Female); // lucavi
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)3, diskConfig.Monk_Male); // northern_sky

            // Unchanged properties should remain original
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)0, diskConfig.Squire_Male); // original
        }

        [Fact]
        public void GetAllJobColors_AfterSettingColors_ShouldReturnCorrectValues()
        {
            // Arrange
            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            var mod = new Mod(_modContext, _inputSimulator);
            mod.InitializeConfiguration(_testConfigPath);

            // Act
            mod.SetJobColor("Knight_Male", "corpse_brigade");
            mod.SetJobColor("Archer_Female", "lucavi");

            var allColors = mod.GetAllJobColors();

            // Assert
            Assert.Equal("Corpse Brigade", allColors["Knight_Male"]);
            Assert.Equal("Lucavi", allColors["Archer_Female"]);
            Assert.Equal("Original", allColors["Squire_Male"]);
            Assert.Equal("Original", allColors["Monk_Male"]);
        }

        [Fact]
        public void LoadConfiguration_FromExistingFile_ShouldApplyColors()
        {
            // Arrange - Create a config file first
            var config = new Config
            {
                Knight_Male = (FFTColorMod.Configuration.ColorScheme)2,    // lucavi
                Monk_Female = (FFTColorMod.Configuration.ColorScheme)1     // corpse_brigade
            };

            var configManager = new ConfigurationManager(_testConfigPath);
            configManager.SaveConfig(config);

            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            // Act - Create new mod instance which should load the config
            var mod = new Mod(_modContext, _inputSimulator);
            mod.InitializeConfiguration(_testConfigPath);

            // Since we're not calling Start(), we need to manually apply the configuration
            var loadedConfig = configManager.LoadConfig();
            mod.ConfigurationUpdated(loadedConfig);

            // Assert
            Assert.Equal("Lucavi", mod.GetJobColor("Knight_Male"));
            Assert.Equal("Corpse Brigade", mod.GetJobColor("Monk_Female"));
            Assert.Equal("Original", mod.GetJobColor("Archer_Female"));
        }

        [Fact]
        public void ResetAllColors_ShouldSetAllToOriginal()
        {
            // Arrange
            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            var mod = new Mod(_modContext, _inputSimulator);
            mod.InitializeConfiguration(_testConfigPath);

            // Set some colors
            mod.SetJobColor("Knight_Male", "corpse_brigade");
            mod.SetJobColor("Archer_Female", "lucavi");

            // Act
            mod.ResetAllColors();

            // Load config directly from disk using a fresh ConfigurationManager
            var freshConfigManager = new ConfigurationManager(_testConfigPath);
            var diskConfig = freshConfigManager.LoadConfig();

            // Assert - verify ALL properties are reset to original (0)
            var properties = typeof(Config).GetProperties()
                .Where(p => p.PropertyType == typeof(FFTColorMod.Configuration.ColorScheme) &&
                           (p.Name.EndsWith("_Male") || p.Name.EndsWith("_Female")));

            foreach (var property in properties)
            {
                var value = property.GetValue(diskConfig);
                Assert.Equal((FFTColorMod.Configuration.ColorScheme)0, value); // All should be original (0)
            }
        }
    }
}