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
                ""SquireMale"": ""southern_sky"",
                ""KnightFemale"": ""emerald_dragon"",
                ""ArcherMale"": ""corpse_brigade"",
                ""WhiteMageMale"": ""northern_sky""
            }";

            // Act - Deserialize from JSON
            var config = JsonSerializer.Deserialize<Config>(json, Configurable<Config>.SerializerOptions);

            // Assert - Properties should be correctly set
            Assert.NotNull(config);
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.southern_sky, config.Squire_Male);
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.emerald_dragon, config.Knight_Female);
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.corpse_brigade, config.Archer_Male);
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.northern_sky, config.WhiteMage_Male);

            // Other properties should have default values
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.original, config.Knight_Male);
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.original, config.Monk_Female);
        }
    }
}