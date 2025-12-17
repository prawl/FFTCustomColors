using System.IO;
using Newtonsoft.Json.Linq;
using FFTColorCustomizer.Configuration;
using Xunit;

namespace FFTColorCustomizer.Tests
{
    public class ConfigSerializationTests
    {
        [Fact]
        public void ConfigurationManager_SavedConfig_ShouldNotContain_FilePath()
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
                    Squire_Male = "northern_sky"
                };

                // Act
                configManager.SaveConfig(config);

                // Assert
                var jsonContent = File.ReadAllText(configPath);
                var jsonObject = JObject.Parse(jsonContent);

                Assert.False(jsonObject.ContainsKey("FilePath"),
                    "Saved config should not contain FilePath property");
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}
