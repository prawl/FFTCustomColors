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

            var mod = new Mod(_modContext, _inputSimulator, new NullHotkeyHandler());

            // IMPORTANT: Call InitializeConfiguration to set up the managers
            mod.InitializeConfiguration(_testConfigPath);

            // Act
            mod.SetJobColor("Archer_Female", "lucavi");

            // Wait for file operations to complete
            System.Threading.Thread.Sleep(200);

            // Assert - verify the color was set correctly
            var color = mod.GetJobColor("Archer_Female");
            Assert.Equal("Lucavi", color);

            // Verify the config was saved
            Assert.True(File.Exists(_testConfigPath), "Config file should exist");
        }

        [Fact]
        public void SetJobColor_MultipleProperties_ShouldSetCorrectly()
        {
            // Arrange
            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            var mod = new Mod(_modContext, _inputSimulator, new NullHotkeyHandler());
            mod.InitializeConfiguration(_testConfigPath);

            // Act - set colors with delays between each
            mod.SetJobColor("Knight_Male", "corpse_brigade");
            System.Threading.Thread.Sleep(100);

            mod.SetJobColor("Archer_Female", "lucavi");
            System.Threading.Thread.Sleep(100);

            mod.SetJobColor("Monk_Male", "northern_sky");
            System.Threading.Thread.Sleep(100);

            // Additional wait for all operations to complete
            System.Threading.Thread.Sleep(200);

            // Assert - verify the colors with retry logic
            var maxRetries = 3;
            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    Assert.Equal("Corpse Brigade", mod.GetJobColor("Knight_Male"));
                    Assert.Equal("Lucavi", mod.GetJobColor("Archer_Female"));
                    Assert.Equal("Northern Sky", mod.GetJobColor("Monk_Male"));
                    break; // All assertions passed
                }
                catch when (retry < maxRetries - 1)
                {
                    // Wait and retry
                    System.Threading.Thread.Sleep(200);
                }
            }

            // Verify the config file exists
            Assert.True(File.Exists(_testConfigPath), "Config file should exist after setting colors");
        }

        [Fact]
        public void GetAllJobColors_AfterSettingColors_ShouldReturnCorrectValues()
        {
            // Arrange
            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            var mod = new Mod(_modContext, _inputSimulator, new NullHotkeyHandler());
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
                Knight_Male = "lucavi",    // lucavi
                Monk_Female = "corpse_brigade"     // corpse_brigade
            };

            var configManager = new ConfigurationManager(_testConfigPath);
            configManager.SaveConfig(config);

            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            // Act - Create new mod instance which should load the config
            var mod = new Mod(_modContext, _inputSimulator, new NullHotkeyHandler());
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

            var mod = new Mod(_modContext, _inputSimulator, new NullHotkeyHandler());
            mod.InitializeConfiguration(_testConfigPath);

            // Set only two colors to keep test simpler
            mod.SetJobColor("Knight_Male", "corpse_brigade");
            mod.SetJobColor("Archer_Female", "lucavi");

            // Add delay to ensure the sets are complete
            System.Threading.Thread.Sleep(100);

            // Verify colors were set correctly
            Assert.Equal("Corpse Brigade", mod.GetJobColor("Knight_Male"));
            Assert.Equal("Lucavi", mod.GetJobColor("Archer_Female"));

            // Act - Reset all colors
            mod.ResetAllColors();

            // Wait for reset operations to complete
            System.Threading.Thread.Sleep(500);

            // Assert - Check specific jobs are reset
            Assert.Equal("Original", mod.GetJobColor("Knight_Male"));
            Assert.Equal("Original", mod.GetJobColor("Archer_Female"));
            Assert.Equal("Original", mod.GetJobColor("Squire_Male"));

            // Verify file content doesn't contain non-original values
            if (File.Exists(_testConfigPath))
            {
                var jsonContent = File.ReadAllText(_testConfigPath);

                // The JSON should not contain any non-original color schemes
                Assert.DoesNotContain("\"corpse_brigade\"", jsonContent);
                Assert.DoesNotContain("\"lucavi\"", jsonContent);

                // Check that if "original" appears or the file is minimal/empty
                // Both are valid for a reset config
                var containsOriginal = jsonContent.Contains("\"original\"");
                var isMinimal = jsonContent.Length < 100 || jsonContent.Trim() == "{}";

                Assert.True(containsOriginal || isMinimal,
                    "Config should either explicitly set 'original' or be minimal/empty after reset");
            }
        }
    }
}