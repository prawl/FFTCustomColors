using System;
using System.IO;
using Xunit;
using FFTColorMod;
using FFTColorMod.Configuration;
using Reloaded.Mod.Interfaces;

namespace FFTColorMod.Tests
{
    public class ReloadedConfigIntegrationTests : IDisposable
    {
        private readonly string _testConfigPath;
        private readonly string _testModPath;

        public ReloadedConfigIntegrationTests()
        {
            _testModPath = Path.Combine(Path.GetTempPath(), $"test_mod_{Guid.NewGuid()}");
            _testConfigPath = Path.Combine(_testModPath, "ModConfig.json");
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
        public void ReloadedConfig_ShouldSerializeToJson()
        {
            // Arrange
            var config = new ReloadedConfig();
            config.KnightMale = "corpse_brigade";
            config.ArcherFemale = "lucavi";

            // Act
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(_testConfigPath, json);

            // Assert
            Assert.Contains("\"KnightMale\": \"corpse_brigade\"", json);
            Assert.Contains("\"ArcherFemale\": \"lucavi\"", json);
        }

        [Fact]
        public void ReloadedConfig_ShouldDeserializeFromJson()
        {
            // Arrange
            var json = @"{
                ""KnightMale"": ""northern_sky"",
                ""MonkFemale"": ""smoke""
            }";
            File.WriteAllText(_testConfigPath, json);

            // Act
            var config = Newtonsoft.Json.JsonConvert.DeserializeObject<ReloadedConfig>(File.ReadAllText(_testConfigPath));

            // Assert
            Assert.Equal("northern_sky", config.KnightMale);
            Assert.Equal("smoke", config.MonkFemale);
            Assert.Equal("original", config.ThiefMale); // Should have default value
        }

        [Fact]
        public void ReloadedConfig_ShouldProvideColorSchemeOptions()
        {
            // Arrange
            var config = new ReloadedConfig();

            // Act
            var schemes = config.GetAvailableColorSchemes();

            // Assert
            Assert.Contains("original", schemes);
            Assert.Contains("corpse_brigade", schemes);
            Assert.Contains("lucavi", schemes);
            Assert.Contains("northern_sky", schemes);
            Assert.Contains("smoke", schemes);
            Assert.Contains("southern_sky", schemes);
        }

        [Fact]
        public void ModConfig_ShouldIntegrateWithReloadedConfig()
        {
            // Arrange
            var modConfig = new ModConfig
            {
                JobColors = new ReloadedConfig
                {
                    KnightMale = "lucavi",
                    DragoonFemale = "corpse_brigade"
                }
            };

            // Act
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(modConfig, Newtonsoft.Json.Formatting.Indented);

            // Assert
            Assert.Contains("JobColors", json);
            Assert.Contains("lucavi", json);
            Assert.Contains("corpse_brigade", json);
        }

        [Fact]
        public void Mod_ShouldLoadReloadedConfig()
        {
            // Arrange
            var config = new ReloadedConfig
            {
                KnightMale = "smoke",
                ArcherFemale = "northern_sky"
            };

            var modConfig = new ModConfig
            {
                JobColors = config
            };

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(modConfig, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(_testConfigPath, json);

            Environment.SetEnvironmentVariable("FFT_RELOADED_CONFIG_PATH", _testConfigPath);

            // Act
            var loadedConfig = ReloadedConfigManager.LoadModConfig(_testConfigPath);

            // Assert
            Assert.Equal("smoke", loadedConfig.JobColors.KnightMale);
            Assert.Equal("northern_sky", loadedConfig.JobColors.ArcherFemale);
        }
    }
}