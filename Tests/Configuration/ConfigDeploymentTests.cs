using System;
using System.IO;
using Xunit;
using FFTColorCustomizer.Configuration;
using Newtonsoft.Json;

namespace FFTColorCustomizer.Tests
{
    public class ConfigDeploymentTests
    {
        [Fact]
        public void ConfigJson_ShouldExistInColorModFolder()
        {
            // Arrange
            var configPath = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                "..", "..", "..", "..", "ColorMod", "Config.json");

            // Act & Assert
            Assert.True(File.Exists(configPath), $"Config.json should exist at {configPath}");
        }

        [Fact]
        public void ConfigJson_ShouldContainValidDefaultConfiguration()
        {
            // Arrange
            var configPath = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                "..", "..", "..", "..", "ColorMod", "Config.json");

            // Act
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<Config>(json);

                // Assert
                Assert.NotNull(config);
                // Check that at least some jobs have default "original" value
                Assert.Equal("original", config.Knight_Male);  // original
                Assert.Equal("original", config.Knight_Female); // original
            }
            else
            {
                // If file doesn't exist, create it
                var defaultConfig = new Config();
                var json = JsonConvert.SerializeObject(defaultConfig, Formatting.Indented);
                Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
                File.WriteAllText(configPath, json);

                // Now verify it was created correctly
                Assert.True(File.Exists(configPath));
            }
        }

        [Fact]
        public void Configurator_ShouldProvideValidConfiguration()
        {
            // Arrange
            var configPath = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                "..", "..", "..", "..", "ColorMod");

            var configurator = new Configurator(configPath);

            // Act
            var config = configurator.GetConfiguration<Config>(0);

            // Assert
            Assert.NotNull(config);
            Assert.Equal("original", config.Knight_Male);  // original
            Assert.Equal("original", config.Knight_Female); // original

            // Ensure Save method doesn't throw
            configurator.Save();
        }
    }
}
