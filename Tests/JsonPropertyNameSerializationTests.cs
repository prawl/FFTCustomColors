using System;
using System.IO;
using System.Text.Json;
using Xunit;
using FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Tests
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
                Squire_Male = "southern_sky",
                Knight_Female = "emerald_dragon",
                WhiteMage_Male = "northern_sky"
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
            Assert.Contains("\"southern_sky\"", json);
            Assert.Contains("\"emerald_dragon\"", json);
            Assert.Contains("\"northern_sky\"", json);
        }
    }
}
