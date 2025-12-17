using System;
using System.IO;
using System.Text.Json;
using Xunit;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Services;
using Newtonsoft.Json;

namespace FFTColorCustomizer.Tests
{
    public class ConfigurationManagerTests : IDisposable
    {
        private readonly string _testConfigPath;
        private readonly ConfigurationManager _configManager;
        private readonly string _tempDataPath;

        public ConfigurationManagerTests()
        {
            _testConfigPath = Path.Combine(Path.GetTempPath(), $"test_config_{Guid.NewGuid()}.json");

            // Set up test data directory with JobClasses.json
            _tempDataPath = SetupTestData();

            _configManager = new ConfigurationManager(_testConfigPath);
        }

        private string SetupTestData()
        {
            // Create test data directory structure
            var tempRoot = Path.Combine(Path.GetTempPath(), "TestMod_" + Guid.NewGuid());
            Directory.CreateDirectory(tempRoot);
            var tempDataPath = Path.Combine(tempRoot, "Data");
            Directory.CreateDirectory(tempDataPath);

            // Create JobClasses.json with all the themes the test expects
            var jobClassesPath = Path.Combine(tempDataPath, "JobClasses.json");
            var jobClassesData = new
            {
                availableThemes = new[]
                {
                    "original",
                    "corpse_brigade",
                    "lucavi",
                    "northern_sky",
                    "southern_sky",
                    "crimson_red",
                    "royal_purple",
                    "phoenix_flame",
                    "frost_knight",
                    "silver_knight",
                    "emerald_dragon",
                    "rose_gold",
                    "ocean_depths",
                    "golden_templar",
                    "blood_moon",
                    "celestial",
                    "volcanic",
                    "amethyst"
                },
                jobClasses = new object[] { }
            };

            File.WriteAllText(jobClassesPath, System.Text.Json.JsonSerializer.Serialize(jobClassesData, new JsonSerializerOptions { WriteIndented = true }));

            // Initialize the JobClassServiceSingleton with our test data path
            JobClassServiceSingleton.Initialize(tempRoot);

            return tempDataPath;
        }

        public void Dispose()
        {
            if (File.Exists(_testConfigPath))
                File.Delete(_testConfigPath);

            if (Directory.Exists(_tempDataPath))
            {
                // Delete the parent directory of Data
                var parentDir = Path.GetDirectoryName(_tempDataPath);
                if (Directory.Exists(parentDir))
                {
                    Directory.Delete(parentDir, true);
                }
            }
        }

        [Fact]
        public void LoadConfig_WhenFileDoesNotExist_ReturnsDefaultConfig()
        {
            // Act
            var config = _configManager.LoadConfig();

            // Assert
            Assert.NotNull(config);
            Assert.Equal("original", config.Knight_Male);
            Assert.Equal("original", config.Archer_Female);
            Assert.Equal("original", config.Monk_Male);
        }

        [Fact]
        public void SaveConfig_CreatesFileWithCorrectContent()
        {
            // Arrange
            var config = new Config
            {
                Knight_Male = "corpse_brigade",
                Archer_Female = "lucavi",
                Monk_Male = "northern_sky"
            };

            // Act
            _configManager.SaveConfig(config);

            // Assert
            Assert.True(File.Exists(_testConfigPath));

            // Load the config back using the ConfigurationManager to use the same JSON settings
            var loadedConfig = _configManager.LoadConfig();

            Assert.Equal("corpse_brigade", loadedConfig.Knight_Male);
            Assert.Equal("lucavi", loadedConfig.Archer_Female);
            Assert.Equal("northern_sky", loadedConfig.Monk_Male);
        }

        [Fact]
        public void LoadConfig_WhenFileExists_ReturnsConfigFromFile()
        {
            // Arrange
            var originalConfig = new Config
            {
                Knight_Male = "southern_sky",
                Archer_Female = "silver_knight",
                WhiteMage_Male = "ocean_depths"
            };
            _configManager.SaveConfig(originalConfig);

            // Act
            var loadedConfig = _configManager.LoadConfig();

            // Assert
            Assert.Equal("southern_sky", loadedConfig.Knight_Male);
            Assert.Equal("silver_knight", loadedConfig.Archer_Female);
            Assert.Equal("ocean_depths", loadedConfig.WhiteMage_Male);
            // Other properties should still have default values
            Assert.Equal("original", loadedConfig.BlackMage_Female); // original - default value
        }

        [Fact]
        public void GetColorSchemeForSprite_ReturnsCorrectColorFromConfig()
        {
            // Arrange
            var config = new Config
            {
                Knight_Male = "royal_purple",
                Dragoon_Female = "phoenix_flame"
            };
            _configManager.SaveConfig(config);

            // Act
            var knightColor = _configManager.GetColorSchemeForSprite("battle_knight_m_spr.bin");
            var dragoonColor = _configManager.GetColorSchemeForSprite("battle_ryu_w_spr.bin");
            var unknownColor = _configManager.GetColorSchemeForSprite("unknown_sprite.bin");

            // Assert
            Assert.Equal("sprites_royal_purple", knightColor);
            Assert.Equal("sprites_phoenix_flame", dragoonColor);
            Assert.Equal("sprites_original", unknownColor);
        }

        [Fact]
        public void SetColorSchemeForJob_UpdatesSpecificJobColor()
        {
            // Arrange
            var config = new Config();
            _configManager.SaveConfig(config);

            // Act
            _configManager.SetColorSchemeForJob("Knight_Male", "frost_knight");
            _configManager.SetColorSchemeForJob("Archer_Female", "silver_knight");

            // Assert
            // Use ReloadConfig to force a fresh read from disk
            var updatedConfig = _configManager.ReloadConfig();
            Assert.Equal("frost_knight", updatedConfig.Knight_Male);
            Assert.Equal("silver_knight", updatedConfig.Archer_Female);
            Assert.Equal("original", updatedConfig.Monk_Male);      // unchanged
        }

        [Fact]
        public void GetAvailableColorSchemes_ReturnsListOfSchemes()
        {
            // Act
            var schemes = _configManager.GetAvailableColorSchemes();

            // Assert
            Assert.NotNull(schemes);
            Assert.Contains("original", schemes);
            Assert.Contains("corpse_brigade", schemes);
            Assert.Contains("lucavi", schemes);
            Assert.Contains("northern_sky", schemes);
            Assert.Contains("southern_sky", schemes);
            Assert.Contains("crimson_red", schemes);
            Assert.Contains("royal_purple", schemes);
            Assert.Contains("phoenix_flame", schemes);
            Assert.Contains("frost_knight", schemes);
            Assert.Contains("silver_knight", schemes);
        }

        [Fact]
        public void ResetToDefaults_ResetsAllColorsToOriginal()
        {
            // Arrange
            var config = new Config
            {
                Knight_Male = "corpse_brigade",
                Archer_Female = "lucavi",
                Monk_Male = "northern_sky",
                Thief_Female = "original"
            };
            _configManager.SaveConfig(config);

            // Act
            _configManager.ResetToDefaults();

            // Assert
            var resetConfig = _configManager.LoadConfig();
            Assert.Equal("original", resetConfig.Knight_Male);
            Assert.Equal("original", resetConfig.Archer_Female); // original
            Assert.Equal("original", resetConfig.Monk_Male);     // original
            Assert.Equal("original", resetConfig.Thief_Female);  // original
        }

        [Fact]
        public void ConfigManager_HandlesInvalidJsonGracefully()
        {
            // Arrange
            File.WriteAllText(_testConfigPath, "{ invalid json content }}}");

            // Act
            var config = _configManager.LoadConfig();

            // Assert
            Assert.NotNull(config);
            Assert.Equal("original", config.Knight_Male); // Should return default config
        }

        [Fact]
        public void GetJobPropertyForSprite_ReturnsCorrectPropertyName()
        {
            // Act & Assert
            Assert.Equal("Knight_Male", _configManager.GetJobPropertyForSprite("battle_knight_m_spr.bin"));
            Assert.Equal("Archer_Female", _configManager.GetJobPropertyForSprite("battle_yumi_w_spr.bin"));
            Assert.Equal("Monk_Male", _configManager.GetJobPropertyForSprite("battle_monk_m_spr.bin"));
            Assert.Null(_configManager.GetJobPropertyForSprite("unknown_sprite.bin"));
        }
    }
}
