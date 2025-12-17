using System;
using System.IO;
using Xunit;
using FFTColorCustomizer.Configuration;
using Newtonsoft.Json;

namespace Tests.Configuration
{
    public class ConfigStoryCharacterPersistenceTests : IDisposable
    {
        private string _tempConfigPath;
        private string _tempDir;

        public ConfigStoryCharacterPersistenceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            _tempConfigPath = Path.Combine(_tempDir, "Config.json");
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Fact]
        public void Config_Should_Serialize_Cloud_Property()
        {
            // Arrange
            var config = new Config
            {
                Cloud = "knights_round"
            };

            // Act
            var json = JsonConvert.SerializeObject(config);

            // Assert - JSON should contain Cloud property
            Assert.Contains("\"Cloud\":\"knights_round\"", json);
        }

        [Fact]
        public void Config_Should_Deserialize_Cloud_Property()
        {
            // Arrange
            var json = @"{
                ""SquireMale"": ""original"",
                ""Cloud"": ""knights_round"",
                ""Agrias"": ""ash_dark"",
                ""Orlandeau"": ""thunder_god""
            }";

            // Act
            var config = JsonConvert.DeserializeObject<Config>(json);

            // Assert
            Assert.NotNull(config);
            Assert.Equal("knights_round", config.Cloud);
            Assert.Equal("ash_dark", config.Agrias);
            Assert.Equal("thunder_god", config.Orlandeau);
        }

        [Fact]
        public void ConfigurationManager_Should_Load_Story_Characters_From_File()
        {
            // Arrange - Write a config file with story characters
            var json = @"{
                ""SquireMale"": ""original"",
                ""SquireFemale"": ""original"",
                ""KnightMale"": ""original"",
                ""KnightFemale"": ""original"",
                ""MonkMale"": ""original"",
                ""MonkFemale"": ""original"",
                ""ArcherMale"": ""original"",
                ""ArcherFemale"": ""original"",
                ""WhiteMageMale"": ""original"",
                ""WhiteMageFemale"": ""original"",
                ""BlackMageMale"": ""original"",
                ""BlackMageFemale"": ""original"",
                ""TimeMageMale"": ""original"",
                ""TimeMageFemale"": ""original"",
                ""SummonerMale"": ""original"",
                ""SummonerFemale"": ""original"",
                ""ThiefMale"": ""original"",
                ""ThiefFemale"": ""original"",
                ""MediatorMale"": ""original"",
                ""MediatorFemale"": ""original"",
                ""MysticMale"": ""original"",
                ""MysticFemale"": ""original"",
                ""GeomancerMale"": ""original"",
                ""GeomancerFemale"": ""original"",
                ""DragoonMale"": ""original"",
                ""DragoonFemale"": ""original"",
                ""SamuraiMale"": ""original"",
                ""SamuraiFemale"": ""original"",
                ""NinjaMale"": ""original"",
                ""NinjaFemale"": ""original"",
                ""CalculatorMale"": ""original"",
                ""CalculatorFemale"": ""original"",
                ""BardMale"": ""original"",
                ""DancerFemale"": ""original"",
                ""MimeMale"": ""original"",
                ""MimeFemale"": ""original"",
                ""ChemistMale"": ""original"",
                ""ChemistFemale"": ""original"",
                ""Cloud"": ""sephiroth_black"",
                ""Agrias"": ""ash_dark"",
                ""Orlandeau"": ""thunder_god""
            }";

            File.WriteAllText(_tempConfigPath, json);

            // Act
            var manager = new ConfigurationManager(_tempConfigPath);
            var config = manager.LoadConfig();

            // Assert
            Assert.Equal("sephiroth_black", config.Cloud);
            Assert.Equal("ash_dark", config.Agrias);
            Assert.Equal("thunder_god", config.Orlandeau);
        }

        [Fact]
        public void ConfigurationManager_Should_Save_And_Reload_Story_Characters()
        {
            // Arrange
            var manager = new ConfigurationManager(_tempConfigPath);
            var config = new Config
            {
                Cloud = "knights_round",
                Agrias = "original",
                Orlandeau = "original"
            };

            // Act - Save
            manager.SaveConfig(config);

            // Act - Reload
            var reloaded = manager.LoadConfig();

            // Assert
            Assert.Equal("knights_round", reloaded.Cloud);
            Assert.Equal("original", reloaded.Agrias);
            Assert.Equal("original", reloaded.Orlandeau);
        }
    }
}
