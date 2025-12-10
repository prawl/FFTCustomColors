using System;
using System.IO;
using System.Text.Json;
using Xunit;
using FFTColorMod.Configuration;

namespace FFTColorMod.Tests
{
    public class JsonPropertyNameDeserializationTests : IDisposable
    {
        private readonly string _testConfigPath;
        private readonly string _testConfigDir;

        public JsonPropertyNameDeserializationTests()
        {
            _testConfigDir = Path.Combine(Path.GetTempPath(), $"test_json_deserialize_{Guid.NewGuid()}");
            _testConfigPath = Path.Combine(_testConfigDir, "Config.json");
            Directory.CreateDirectory(_testConfigDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testConfigDir))
                Directory.Delete(_testConfigDir, true);
        }

        [Fact]
        public void Config_DeserializesFromJsonWithoutUnderscores_ToCorrectProperties()
        {
            // Arrange - JSON with no underscores (like what Reloaded-II saves)
            var json = @"{
                ""SquireMale"": ""smoke"",
                ""KnightFemale"": ""emerald_dragon"",
                ""ArcherMale"": ""corpse_brigade"",
                ""WhiteMageMale"": ""northern_sky""
            }";

            // Act - Deserialize from JSON
            var config = JsonSerializer.Deserialize<Config>(json, Configurable<Config>.SerializerOptions);

            // Assert - Properties should be correctly set
            Assert.NotNull(config);
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)4, config.Squire_Male);  // smoke
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)12, config.Knight_Female); // emerald_dragon
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)1, config.Archer_Male);   // corpse_brigade
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)3, config.WhiteMage_Male); // northern_sky

            // Other properties should have default values
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)0, config.Knight_Male);    // original
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)0, config.Monk_Female);    // original
        }
    }
}