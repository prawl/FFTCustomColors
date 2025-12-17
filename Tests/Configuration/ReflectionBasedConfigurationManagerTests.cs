using System.IO;
using Newtonsoft.Json.Linq;
using FFTColorCustomizer.Configuration;
using Xunit;
using System;

namespace FFTColorCustomizer.Tests
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
                    Agrias = "ash_dark",
                    Cloud = "sephiroth_black",
                    Orlandeau = "thunder_god"
                };

                // Act
                configManager.SaveConfig(config);
                var jsonContent = File.ReadAllText(configPath);
                var jsonObject = JObject.Parse(jsonContent);

                // Assert - All story characters should be saved
                Assert.Equal("ash_dark", jsonObject["Agrias"]?.ToString());
                Assert.Equal("sephiroth_black", jsonObject["Cloud"]?.ToString());
                Assert.Equal("thunder_god", jsonObject["Orlandeau"]?.ToString());
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
                    ""Agrias"": ""ash_dark"",
                    ""Cloud"": ""sephiroth_black"",
                    ""Orlandeau"": ""original"",
                    ""Mustadio"": ""original"",
                    ""Reis"": ""original""
                }";
                File.WriteAllText(configPath, jsonContent);

                var configManager = new ConfigurationManager(configPath);

                // Act
                var config = configManager.LoadConfig();

                // Assert - All story characters should be loaded correctly
                Assert.Equal("ash_dark", config.Agrias);
                Assert.Equal("sephiroth_black", config.Cloud);
                Assert.Equal("original", config.Orlandeau);
                Assert.Equal("original", config.Mustadio);
                Assert.Equal("original", config.Reis);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}
