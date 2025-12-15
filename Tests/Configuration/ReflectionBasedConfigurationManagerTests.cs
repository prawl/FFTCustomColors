using System.IO;
using Newtonsoft.Json.Linq;
using FFTColorMod.Configuration;
using Xunit;
using System;

namespace FFTColorMod.Tests
{
    public class ReflectionBasedConfigurationManagerTests
    {
        [Fact]
        public void ConfigurationManager_Should_Save_All_Story_Characters_Without_Hardcoding()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            var configPath = Path.Combine(tempDir, "Config.json");

            try
            {
                var configManager = new ConfigurationManager(configPath);
                var config = new Config
                {
                    // Set some story characters
                    Alma = AlmaColorScheme.crimson_red,
                    Celia = CeliaColorScheme.forest_green,
                    Delita = DelitaColorScheme.midnight_black,
                    Reis = ReisColorScheme.sunset_orange
                };

                // Act
                configManager.SaveConfig(config);
                var jsonContent = File.ReadAllText(configPath);
                var jsonObject = JObject.Parse(jsonContent);

                // Assert - All story characters should be saved
                Assert.Equal("crimson_red", jsonObject["Alma"]?.ToString());
                Assert.Equal("forest_green", jsonObject["Celia"]?.ToString());
                Assert.Equal("midnight_black", jsonObject["Delita"]?.ToString());
                Assert.Equal("sunset_orange", jsonObject["Reis"]?.ToString());
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void ConfigurationManager_Should_Load_All_Story_Characters_Without_Hardcoding()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            var configPath = Path.Combine(tempDir, "Config.json");

            try
            {
                // Create a config file with story characters
                var jsonContent = @"{
                    ""Alma"": ""crimson_red"",
                    ""Celia"": ""forest_green"",
                    ""Delita"": ""midnight_black"",
                    ""Lettie"": ""royal_blue"",
                    ""Malak"": ""golden_yellow""
                }";
                File.WriteAllText(configPath, jsonContent);

                var configManager = new ConfigurationManager(configPath);

                // Act
                var config = configManager.LoadConfig();

                // Assert - All story characters should be loaded correctly
                Assert.Equal(AlmaColorScheme.crimson_red, config.Alma);
                Assert.Equal(CeliaColorScheme.forest_green, config.Celia);
                Assert.Equal(DelitaColorScheme.midnight_black, config.Delita);
                Assert.Equal(LettieColorScheme.royal_blue, config.Lettie);
                Assert.Equal(MalakColorScheme.golden_yellow, config.Malak);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}