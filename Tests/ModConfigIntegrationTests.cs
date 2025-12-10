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
            _testConfigPath = Path.Combine(_testModPath, "config.json");

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

            // Set knight to corpse_brigade via config
            mod.SetJobColor("KnightMale", "corpse_brigade");

            // Act
            var interceptedPath = mod.InterceptFilePath(@"C:\Game\data\battle_knight_m_spr.bin");

            // Assert
            Assert.Contains("sprites_corpse_brigade", interceptedPath);
        }

        [Fact]
        public void Mod_SetJobColor_UpdatesConfiguration()
        {
            // Arrange
            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            var mod = new Mod(_modContext, _inputSimulator);

            // Act
            mod.SetJobColor("ArcherFemale", "lucavi");
            var color = mod.GetJobColor("ArcherFemale");

            // Assert
            Assert.Equal("lucavi", color);
        }

        [Fact]
        public void Mod_GetAllJobColors_ReturnsConfiguredColors()
        {
            // Arrange
            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            var mod = new Mod(_modContext, _inputSimulator);
            mod.SetJobColor("KnightMale", "corpse_brigade");
            mod.SetJobColor("ArcherFemale", "lucavi");

            // Act
            var jobColors = mod.GetAllJobColors();

            // Assert
            Assert.Equal("corpse_brigade", jobColors["KnightMale"]);
            Assert.Equal("lucavi", jobColors["ArcherFemale"]);
            Assert.Equal("original", jobColors["MonkMale"]);
        }

        [Fact]
        public void Mod_F3Hotkey_OpensConfigurationUI()
        {
            // Arrange
            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            var mod = new Mod(_modContext, _inputSimulator);
            const int VK_F3 = 0x72;

            // Act
            mod.ProcessHotkeyPress(VK_F3);

            // Assert - Check if config UI was triggered (would open in-game menu)
            Assert.True(mod.IsConfigUIRequested());
        }

        [Fact]
        public void Mod_ApplyConfigOnStartup_LoadsSavedConfiguration()
        {
            // Arrange - Create a config file first
            var config = new Config
            {
                KnightMale = "lucavi",
                MonkFemale = "corpse_brigade"
            };

            var configManager = new ConfigurationManager(_testConfigPath);
            configManager.SaveConfig(config);

            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            // Act
            var mod = new Mod(_modContext, _inputSimulator);

            // Assert - Configuration should be loaded
            Assert.Equal("lucavi", mod.GetJobColor("KnightMale"));
            Assert.Equal("corpse_brigade", mod.GetJobColor("MonkFemale"));
        }

        [Fact]
        public void Mod_ResetToDefaults_ResetsAllJobColors()
        {
            // Arrange
            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            var mod = new Mod(_modContext, _inputSimulator);
            mod.SetJobColor("KnightMale", "corpse_brigade");
            mod.SetJobColor("ArcherFemale", "lucavi");

            // Act
            mod.ResetAllColors();

            // Assert
            Assert.Equal("original", mod.GetJobColor("KnightMale"));
            Assert.Equal("original", mod.GetJobColor("ArcherFemale"));
        }
    }

    // Test helper class
    public class TestInputSimulator : IInputSimulator
    {
        public bool SendKeyPress(int vkCode) => true;
        public bool SimulateMenuRefresh() => true;
    }
}