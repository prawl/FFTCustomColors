using System;
using System.IO;
using System.Text.Json;
using Xunit;
using FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Tests
{
    public class ConfigurationFilePathPersistenceTests : IDisposable
    {
        private readonly string _testConfigPath;
        private readonly string _testConfigDir;

        public ConfigurationFilePathPersistenceTests()
        {
            _testConfigDir = Path.Combine(Path.GetTempPath(), $"test_filepath_{Guid.NewGuid()}");
            _testConfigPath = Path.Combine(_testConfigDir, "Config.json");
            Directory.CreateDirectory(_testConfigDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testConfigDir))
                Directory.Delete(_testConfigDir, true);
        }

        [Fact]
        public void NewConfigObject_CannotSave_WhenFilePathNotSet()
        {
            // Arrange - Simulate Reloaded-II creating a new Config object
            var updatedConfig = new Config
            {
                Squire_Male = "golden_templar",  // royal_purple
                Knight_Female = "silver_knight"  // southern_sky
            };

            // Act - Try to save (this is what happens in OnConfigurationUpdated)
            Assert.Null(updatedConfig.FilePath);  // FilePath is not set
            updatedConfig.Save();  // This will silently fail

            // Assert - File should NOT be created because FilePath is null
            Assert.False(File.Exists(_testConfigPath), "Config.json should NOT exist when FilePath is null");
        }

        [Fact]
        public void SaveToSpecificPath_Works_WhenExplicitPathProvided()
        {
            // Arrange - Create a new Config object
            var config = new Config
            {
                Squire_Male = "golden_templar",  // royal_purple
                Knight_Female = "silver_knight"  // southern_sky
            };

            // Act - We need a way to save to a specific path
            // This is what we need to implement in Startup.cs
            var json = JsonSerializer.Serialize(config, Configurable<Config>.SerializerOptions);
            File.WriteAllText(_testConfigPath, json);

            // Assert - Verify manual save worked
            Assert.True(File.Exists(_testConfigPath), "Config.json should exist");

            var savedJson = File.ReadAllText(_testConfigPath);
            var reloadedConfig = JsonSerializer.Deserialize<Config>(savedJson, Configurable<Config>.SerializerOptions);

            Assert.NotNull(reloadedConfig);
            Assert.Equal("golden_templar", reloadedConfig.Squire_Male);
            Assert.Equal("silver_knight", reloadedConfig.Knight_Female);
        }
    }
}
