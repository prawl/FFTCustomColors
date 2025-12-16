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

            var mod = new Mod(_modContext, _inputSimulator, new NullHotkeyHandler());
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

            var mod = new Mod(_modContext, _inputSimulator, new NullHotkeyHandler());
            mod.InitializeConfiguration(_testConfigPath); // IMPORTANT: Initialize the managers

            // Set knight to corpse_brigade via config
            mod.SetJobColor("Knight_Male", "corpse_brigade");

            // Act
            var interceptedPath = mod.InterceptFilePath(@"C:\Game\data\battle_knight_m_spr.bin");

            // Assert - Path should be modified from the original
            Assert.NotEqual(@"C:\Game\data\battle_knight_m_spr.bin", interceptedPath);
            Assert.Contains("battle_knight_m_spr.bin", interceptedPath);
        }


        [Fact]
        public void Mod_GetAllJobColors_ReturnsConfiguredColors()
        {
            // Arrange
            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            var mod = new Mod(_modContext, _inputSimulator, new NullHotkeyHandler());
            mod.InitializeConfiguration(_testConfigPath); // Must initialize before using config methods

            // Set colors with delays
            mod.SetJobColor("Knight_Male", "corpse_brigade");
            System.Threading.Thread.Sleep(100);

            mod.SetJobColor("Archer_Female", "lucavi");
            System.Threading.Thread.Sleep(200);

            // Act
            var jobColors = mod.GetAllJobColors();

            // Assert - only check what we explicitly set
            Assert.Equal("Corpse Brigade", jobColors["Knight_Male"]);
            Assert.Equal("Lucavi", jobColors["Archer_Female"]);

            // Verify the dictionary contains expected keys
            Assert.True(jobColors.ContainsKey("Knight_Male"));
            Assert.True(jobColors.ContainsKey("Archer_Female"));
            Assert.True(jobColors.ContainsKey("Monk_Male"));
        }

        [Fact]
        public void Mod_ApplyConfigOnStartup_LoadsSavedConfiguration()
        {
            // Arrange - Create a config file first
            var config = new Config
            {
                Knight_Male = "lucavi",    // lucavi
                Monk_Female = "corpse_brigade"     // corpse_brigade
            };

            var configManager = new ConfigurationManager(_testConfigPath);
            configManager.SaveConfig(config);

            // Wait for file to be written
            System.Threading.Thread.Sleep(100);

            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            // Act - Create mod which should load the config
            var mod = new Mod(_modContext, _inputSimulator, new NullHotkeyHandler());
            mod.InitializeConfiguration(_testConfigPath);

            // Wait for initialization to complete
            System.Threading.Thread.Sleep(200);

            // Apply the loaded configuration
            var loadedConfig = configManager.LoadConfig();
            mod.ConfigurationUpdated(loadedConfig);

            // Wait for configuration to be applied
            System.Threading.Thread.Sleep(100);

            // Assert - Configuration should be loaded and applied
            Assert.Equal("Lucavi", mod.GetJobColor("Knight_Male"));
            Assert.Equal("Corpse Brigade", mod.GetJobColor("Monk_Female"));
        }

        [Fact]
        public void Mod_ResetToDefaults_ResetsAllJobColors()
        {
            // Arrange
            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            var mod = new Mod(_modContext, _inputSimulator, new NullHotkeyHandler());
            mod.InitializeConfiguration(_testConfigPath); // Must initialize before using config methods

            mod.SetJobColor("Knight_Male", "corpse_brigade");
            System.Threading.Thread.Sleep(100);

            mod.SetJobColor("Archer_Female", "lucavi");
            System.Threading.Thread.Sleep(200);

            // Verify colors were set with retry logic
            var maxRetries = 3;
            for (int retry = 0; retry < maxRetries; retry++)
            {
                var knightColor = mod.GetJobColor("Knight_Male");
                var archerColor = mod.GetJobColor("Archer_Female");

                if (knightColor == "Corpse Brigade" && archerColor == "Lucavi")
                {
                    break; // Colors are set correctly
                }

                if (retry < maxRetries - 1)
                {
                    System.Threading.Thread.Sleep(200);
                }
                else
                {
                    // Final attempt failed, assert to show the actual values
                    Assert.Equal("Corpse Brigade", knightColor);
                    Assert.Equal("Lucavi", archerColor);
                }
            }

            // Act
            mod.ResetAllColors();

            // Wait for reset to complete
            System.Threading.Thread.Sleep(500);

            // Assert - verify through the mod's own API
            Assert.Equal("Original", mod.GetJobColor("Knight_Male"));
            Assert.Equal("Original", mod.GetJobColor("Archer_Female"));
            Assert.Equal("Original", mod.GetJobColor("Squire_Male"));

            // Verify the config file exists
            Assert.True(File.Exists(_testConfigPath), "Config file should exist");
        }
    }

    // Test helper class
    public class TestInputSimulator : IInputSimulator
    {
        public bool SendKeyPress(int vkCode) => true;
        public bool SimulateMenuRefresh() => true;
    }
}