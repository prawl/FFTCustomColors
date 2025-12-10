using System;
using System.IO;
using System.Text.Json;
using Xunit;
using FFTColorMod.Configuration;

namespace FFTColorMod.Tests
{
    public class JsonPropertyNameSerializationTests : IDisposable
    {
        private readonly string _testConfigPath;
        private readonly string _testConfigDir;

        public JsonPropertyNameSerializationTests()
        {
            _testConfigDir = Path.Combine(Path.GetTempPath(), $"test_json_prop_{Guid.NewGuid()}");
            _testConfigPath = Path.Combine(_testConfigDir, "Config.json");
            Directory.CreateDirectory(_testConfigDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testConfigDir))
                Directory.Delete(_testConfigDir, true);
        }

        [Fact]
        public void Config_SerializesToJsonWithoutUnderscores_WhenSaved()
        {
            // Arrange
            var config = new Config
            {
                Squire_Male = (FFTColorMod.Configuration.ColorScheme)4,  // smoke
                Knight_Female = (FFTColorMod.Configuration.ColorScheme)12, // emerald_dragon
                WhiteMage_Male = (FFTColorMod.Configuration.ColorScheme)3  // northern_sky
            };

            // Act - Serialize to JSON
            var json = JsonSerializer.Serialize(config, Configurable<Config>.SerializerOptions);

            // Assert - JSON should have property names WITHOUT underscores
            Assert.Contains("\"SquireMale\":", json);
            Assert.Contains("\"KnightFemale\":", json);
            Assert.Contains("\"WhiteMageMale\":", json);

            // Should NOT contain underscore versions
            Assert.DoesNotContain("\"Squire_Male\":", json);
            Assert.DoesNotContain("\"Knight_Female\":", json);
            Assert.DoesNotContain("\"WhiteMage_Male\":", json);

            // Values should be enum names (strings), not numbers
            Assert.Contains("\"smoke\"", json);
            Assert.Contains("\"emerald_dragon\"", json);
            Assert.Contains("\"northern_sky\"", json);
        }
    }
}