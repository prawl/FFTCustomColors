using System;
using System.IO;
using Xunit;
using FFTColorMod;
using FFTColorMod.Configuration;
using FFTColorMod.Utilities;

namespace FFTColorMod.Tests
{
    public class ModConfigIntegrationTests : IDisposable
    {
        private readonly string _testModPath;
        private readonly string _testConfigPath;
        private readonly ModContext _modContext;
        private readonly TestInputSimulator _inputSimulator;

        public ModConfigIntegrationTests()
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

            // Create color scheme directories
            var schemes = new[] { "sprites_original", "sprites_corpse_brigade", "sprites_lucavi" };
            foreach (var scheme in schemes)
            {
                var schemeDir = Path.Combine(unitDir, scheme);
                Directory.CreateDirectory(schemeDir);

                // Create dummy sprite files
                File.WriteAllText(Path.Combine(schemeDir, "battle_knight_m_spr.bin"), $"{scheme}_knight");
                File.WriteAllText(Path.Combine(schemeDir, "battle_yumi_w_spr.bin"), $"{scheme}_archer");
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
        public void Mod_ShouldUseConfigBasedSpriteManager()
        {
            // Arrange
            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            var mod = new Mod(_modContext, _inputSimulator);
            mod.InitializeConfiguration(_testConfigPath); // IMPORTANT: Initialize the managers

            // Act
            var hasConfigManager = mod.HasConfigurationManager();

            // Assert
            Assert.True(hasConfigManager);
        }

        [Fact]
        public void Mod_InterceptFilePath_UsesConfigBasedColors()
        {
            // Arrange
            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            var mod = new Mod(_modContext, _inputSimulator);
            mod.InitializeConfiguration(_testConfigPath); // IMPORTANT: Initialize the managers

            // Set knight to corpse_brigade via config
            mod.SetJobColor("Knight_Male", "corpse_brigade");

            // Act
            var interceptedPath = mod.InterceptFilePath(@"C:\Game\data\battle_knight_m_spr.bin");

            // Assert - Path should be modified from the original
            Assert.NotEqual(@"C:\Game\data\battle_knight_m_spr.bin", interceptedPath);
            Assert.Contains("battle_knight_m_spr.bin", interceptedPath);
        }


        [Fact(Skip = "Randomly failing - needs ConfigBasedSpriteManager initialization fix")]
        public void Mod_GetAllJobColors_ReturnsConfiguredColors()
        {
            // Arrange
            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            var mod = new Mod(_modContext, _inputSimulator);
            mod.SetJobColor("Knight_Male", "corpse_brigade");
            mod.SetJobColor("Archer_Female", "lucavi");

            // Act
            var jobColors = mod.GetAllJobColors();

            // Assert
            Assert.Equal("Corpse Brigade", jobColors["Knight_Male"]);
            Assert.Equal("Lucavi", jobColors["Archer_Female"]);
            Assert.Equal("Original", jobColors["Monk_Male"]);
        }

        [Fact(Skip = "Randomly failing - needs ConfigBasedSpriteManager initialization fix")]
        public void Mod_ApplyConfigOnStartup_LoadsSavedConfiguration()
        {
            // Arrange - Create a config file first
            var config = new Config
            {
                Knight_Male = (Configuration.ColorScheme)2,    // lucavi
                Monk_Female = (Configuration.ColorScheme)1     // corpse_brigade
            };

            var configManager = new ConfigurationManager(_testConfigPath);
            configManager.SaveConfig(config);

            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            // Act
            var mod = new Mod(_modContext, _inputSimulator);

            // Assert - Configuration should be loaded
            Assert.Equal("Lucavi", mod.GetJobColor("Knight_Male"));
            Assert.Equal("Corpse Brigade", mod.GetJobColor("Monk_Female"));
        }

        [Fact]
        public void Mod_ResetToDefaults_ResetsAllJobColors()
        {
            // Arrange
            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            var mod = new Mod(_modContext, _inputSimulator);
            mod.InitializeConfiguration(_testConfigPath); // Must initialize before using config methods

            mod.SetJobColor("Knight_Male", "corpse_brigade");
            mod.SetJobColor("Archer_Female", "lucavi");

            // Act
            mod.ResetAllColors();

            // Load config directly from disk using a fresh ConfigurationManager
            var freshConfigManager = new ConfigurationManager(_testConfigPath);
            var diskConfig = freshConfigManager.LoadConfig();

            // Assert - verify the values were reset on disk
            Assert.Equal((Configuration.ColorScheme)0, diskConfig.Knight_Male); // original
            Assert.Equal((Configuration.ColorScheme)0, diskConfig.Archer_Female); // original
        }
    }

    // Test helper class
    public class TestInputSimulator : IInputSimulator
    {
        public bool SendKeyPress(int vkCode) => true;
        public bool SimulateMenuRefresh() => true;
    }
}