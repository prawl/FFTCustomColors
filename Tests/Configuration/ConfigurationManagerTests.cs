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
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)0, config.Knight_Male);
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)0, config.Archer_Female);
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)0, config.Monk_Male);
        }

        [Fact]
        public void SaveConfig_CreatesFileWithCorrectContent()
        {
            // Arrange
            var config = new Config
            {
                Knight_Male = (FFTColorMod.Configuration.ColorScheme)1,
                Archer_Female = (FFTColorMod.Configuration.ColorScheme)2,
                Monk_Male = (FFTColorMod.Configuration.ColorScheme)3
            };

            // Act
            _configManager.SaveConfig(config);

            // Assert
            Assert.True(File.Exists(_testConfigPath));
            var savedContent = File.ReadAllText(_testConfigPath);
            var loadedConfig = JsonConvert.DeserializeObject<Config>(savedContent);

            Assert.Equal((FFTColorMod.Configuration.ColorScheme)1, loadedConfig.Knight_Male);
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)2, loadedConfig.Archer_Female);
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)3, loadedConfig.Monk_Male);
        }

        [Fact]
        public void LoadConfig_WhenFileExists_ReturnsConfigFromFile()
        {
            // Arrange
            var originalConfig = new Config
            {
                Knight_Male = (FFTColorMod.Configuration.ColorScheme)4,
                Archer_Female = (FFTColorMod.Configuration.ColorScheme)5,
                WhiteMage_Male = (FFTColorMod.Configuration.ColorScheme)6
            };
            _configManager.SaveConfig(originalConfig);

            // Act
            var loadedConfig = _configManager.LoadConfig();

            // Assert
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)4, loadedConfig.Knight_Male);
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)5, loadedConfig.Archer_Female);
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)6, loadedConfig.WhiteMage_Male);
            // Other properties should still have default values
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)0, loadedConfig.BlackMage_Female); // original - default value
        }

        [Fact]
        public void GetColorSchemeForSprite_ReturnsCorrectColorFromConfig()
        {
            // Arrange
            var config = new Config
            {
                Knight_Male = FFTColorMod.Configuration.ColorScheme.royal_purple,
                Dragoon_Female = FFTColorMod.Configuration.ColorScheme.phoenix_flame
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
            var updatedConfig = _configManager.LoadConfig();
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.frost_knight, updatedConfig.Knight_Male);
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.silver_knight, updatedConfig.Archer_Female);
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.original, updatedConfig.Monk_Male);      // unchanged
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
            // smoke theme removed - no longer in list
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
                Knight_Male = FFTColorMod.Configuration.ColorScheme.corpse_brigade,
                Archer_Female = FFTColorMod.Configuration.ColorScheme.lucavi,
                Monk_Male = FFTColorMod.Configuration.ColorScheme.northern_sky,
                Thief_Female = FFTColorMod.Configuration.ColorScheme.original
            };
            _configManager.SaveConfig(config);

            // Act
            _configManager.ResetToDefaults();

            // Assert
            var resetConfig = _configManager.LoadConfig();
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.original, resetConfig.Knight_Male);
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)0, resetConfig.Archer_Female); // original
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)0, resetConfig.Monk_Male);     // original
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)0, resetConfig.Thief_Female);  // original
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
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)0, config.Knight_Male); // Should return default config
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