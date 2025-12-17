using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Xunit;
using FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Tests
{
    public class ConfigurationUpdateSaveTests : IDisposable
    {
        private readonly string _testConfigPath;
        private readonly string _testConfigDir;

        public ConfigurationUpdateSaveTests()
        {
            _testConfigDir = Path.Combine(Path.GetTempPath(), $"test_config_save_{Guid.NewGuid()}");
            _testConfigPath = Path.Combine(_testConfigDir, "Config.json");
            Directory.CreateDirectory(_testConfigDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testConfigDir))
                Directory.Delete(_testConfigDir, true);
        }

        [Fact]
        public void ConfigurationUpdate_SavesToDisk_WhenSaveMethodCalled()
        {
            // Arrange - Create config through ConfiguratorMixin
            var configuratorMixin = new ConfiguratorMixin();
            var configurations = configuratorMixin.MakeConfigurations(_testConfigDir);
            var config = configurations[0] as Config;
            Assert.NotNull(config);

            // Act - Modify config and save
            config.Squire_Male = "southern_sky";
            config.Dragoon_Female = "lucavi";
            config.Ninja_Male = "emerald_dragon";

            config.Save();

            // Small delay to ensure file write completes
            Thread.Sleep(100);

            // Assert - Verify the file was updated with new values
            Assert.True(File.Exists(_testConfigPath), "Config.json should exist");

            var savedJson = File.ReadAllText(_testConfigPath);
            var jsonDoc = JsonDocument.Parse(savedJson);

            // Check that JSON has correct property names (no underscores)
            Assert.True(jsonDoc.RootElement.TryGetProperty("SquireMale", out var squireValue));
            Assert.Equal("southern_sky", squireValue.GetString());

            Assert.True(jsonDoc.RootElement.TryGetProperty("DragoonFemale", out var dragoonValue));
            Assert.Equal("lucavi", dragoonValue.GetString());

            Assert.True(jsonDoc.RootElement.TryGetProperty("NinjaMale", out var ninjaValue));
            Assert.Equal("emerald_dragon", ninjaValue.GetString());
        }
    }
}
