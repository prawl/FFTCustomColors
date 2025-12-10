using System;
using System.IO;
using Xunit;
using FFTColorMod.Configuration;
using Newtonsoft.Json;

namespace FFTColorMod.Tests
{
    public class ConfigurationManagerTests : IDisposable
    {
        private readonly string _testConfigPath;
        private readonly ConfigurationManager _configManager;

        public ConfigurationManagerTests()
        {
            _testConfigPath = Path.Combine(Path.GetTempPath(), $"test_config_{Guid.NewGuid()}.json");
            _configManager = new ConfigurationManager(_testConfigPath);
        }

        public void Dispose()
        {
            if (File.Exists(_testConfigPath))
                File.Delete(_testConfigPath);
        }

        [Fact]
        public void LoadConfig_WhenFileDoesNotExist_ReturnsDefaultConfig()
        {
            // Act
            var config = _configManager.LoadConfig();

            // Assert
            Assert.NotNull(config);
            Assert.Equal("original", config.KnightMale);
            Assert.Equal("original", config.ArcherFemale);
            Assert.Equal("original", config.MonkMale);
        }

        [Fact]
        public void SaveConfig_CreatesFileWithCorrectContent()
        {
            // Arrange
            var config = new Config
            {
                KnightMale = "corpse_brigade",
                ArcherFemale = "lucavi",
                MonkMale = "northern_sky"
            };

            // Act
            _configManager.SaveConfig(config);

            // Assert
            Assert.True(File.Exists(_testConfigPath));
            var savedContent = File.ReadAllText(_testConfigPath);
            var loadedConfig = JsonConvert.DeserializeObject<Config>(savedContent);

            Assert.Equal("corpse_brigade", loadedConfig.KnightMale);
            Assert.Equal("lucavi", loadedConfig.ArcherFemale);
            Assert.Equal("northern_sky", loadedConfig.MonkMale);
        }

        [Fact]
        public void LoadConfig_WhenFileExists_ReturnsConfigFromFile()
        {
            // Arrange
            var originalConfig = new Config
            {
                KnightMale = "smoke",
                ArcherFemale = "southern_sky",
                WhiteMageMale = "crimson_red"
            };
            _configManager.SaveConfig(originalConfig);

            // Act
            var loadedConfig = _configManager.LoadConfig();

            // Assert
            Assert.Equal("smoke", loadedConfig.KnightMale);
            Assert.Equal("southern_sky", loadedConfig.ArcherFemale);
            Assert.Equal("crimson_red", loadedConfig.WhiteMageMale);
            // Other properties should still have default values
            Assert.Equal("original", loadedConfig.BlackMageFemale);
        }

        [Fact]
        public void GetColorSchemeForSprite_ReturnsCorrectColorFromConfig()
        {
            // Arrange
            var config = new Config
            {
                KnightMale = "royal_purple",
                DragoonFemale = "phoenix_flame"
            };
            _configManager.SaveConfig(config);

            // Act
            var knightColor = _configManager.GetColorSchemeForSprite("battle_knight_m_spr.bin");
            var dragoonColor = _configManager.GetColorSchemeForSprite("battle_ryu_w_spr.bin");
            var unknownColor = _configManager.GetColorSchemeForSprite("unknown_sprite.bin");

            // Assert
            Assert.Equal("royal_purple", knightColor);
            Assert.Equal("phoenix_flame", dragoonColor);
            Assert.Equal("original", unknownColor);
        }

        [Fact]
        public void SetColorSchemeForJob_UpdatesSpecificJobColor()
        {
            // Arrange
            var config = new Config();
            _configManager.SaveConfig(config);

            // Act
            _configManager.SetColorSchemeForJob("KnightMale", "frost_knight");
            _configManager.SetColorSchemeForJob("ArcherFemale", "silver_knight");

            // Assert
            var updatedConfig = _configManager.LoadConfig();
            Assert.Equal("frost_knight", updatedConfig.KnightMale);
            Assert.Equal("silver_knight", updatedConfig.ArcherFemale);
            Assert.Equal("original", updatedConfig.MonkMale); // Should remain unchanged
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
            Assert.Contains("smoke", schemes);
            // Custom schemes
            Assert.Contains("crimson_red", schemes);
            Assert.Contains("royal_purple", schemes);
            Assert.Contains("phoenix_flame", schemes);
        }

        [Fact]
        public void ResetToDefaults_ResetsAllColorsToOriginal()
        {
            // Arrange
            var config = new Config
            {
                KnightMale = "corpse_brigade",
                ArcherFemale = "lucavi",
                MonkMale = "northern_sky",
                ThiefFemale = "smoke"
            };
            _configManager.SaveConfig(config);

            // Act
            _configManager.ResetToDefaults();

            // Assert
            var resetConfig = _configManager.LoadConfig();
            Assert.Equal("original", resetConfig.KnightMale);
            Assert.Equal("original", resetConfig.ArcherFemale);
            Assert.Equal("original", resetConfig.MonkMale);
            Assert.Equal("original", resetConfig.ThiefFemale);
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
            Assert.Equal("original", config.KnightMale); // Should return default config
        }

        [Fact]
        public void GetJobPropertyForSprite_ReturnsCorrectPropertyName()
        {
            // Act & Assert
            Assert.Equal("KnightMale", _configManager.GetJobPropertyForSprite("battle_knight_m_spr.bin"));
            Assert.Equal("ArcherFemale", _configManager.GetJobPropertyForSprite("battle_yumi_w_spr.bin"));
            Assert.Equal("MonkMale", _configManager.GetJobPropertyForSprite("battle_monk_m_spr.bin"));
            Assert.Null(_configManager.GetJobPropertyForSprite("unknown_sprite.bin"));
        }
    }
}