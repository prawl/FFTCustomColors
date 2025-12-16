using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Xunit;
using FFTColorMod.Configuration;

namespace FFTColorMod.Tests
{
    public class ConfigurationRoundTripTests : IDisposable
    {
        private readonly string _testConfigPath;
        private readonly string _testConfigDir;

        public ConfigurationRoundTripTests()
        {
            _testConfigDir = Path.Combine(Path.GetTempPath(), $"test_roundtrip_{Guid.NewGuid()}");
            _testConfigPath = Path.Combine(_testConfigDir, "Config.json");
            Directory.CreateDirectory(_testConfigDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testConfigDir))
                Directory.Delete(_testConfigDir, true);
        }

        [Fact]
        public void Configuration_RoundTrip_PreservesAllValues()
        {
            // Arrange - Create initial JSON with specific values (simulating Reloaded-II config)
            var initialJson = @"{
                ""SquireMale"": ""southern_sky"",
                ""KnightMale"": ""rose_gold"",
                ""KnightFemale"": ""emerald_dragon"",
                ""ArcherMale"": ""corpse_brigade"",
                ""BlackMageFemale"": ""royal_purple"",
                ""DragoonMale"": ""frost_knight"",
                ""NinjaFemale"": ""volcanic""
            }";

            File.WriteAllText(_testConfigPath, initialJson);

            // Act - Load config, then save it back
            var configuratorMixin = new ConfiguratorMixin();
            var configurations = configuratorMixin.MakeConfigurations(_testConfigDir);
            var config = configurations[0] as Config;
            Assert.NotNull(config);

            // Verify it loaded correctly
            Assert.Equal("southern_sky", config.Squire_Male);
            Assert.Equal("rose_gold", config.Knight_Male);
            Assert.Equal("emerald_dragon", config.Knight_Female);

            // Save it back
            config.Save();
            Thread.Sleep(100); // Ensure file write completes

            // Assert - Reload and verify all values preserved
            var savedJson = File.ReadAllText(_testConfigPath);
            var reloadedConfig = JsonSerializer.Deserialize<Config>(savedJson, Configurable<Config>.SerializerOptions);

            Assert.NotNull(reloadedConfig);
            Assert.Equal(config.Squire_Male, reloadedConfig.Squire_Male);
            Assert.Equal(config.Knight_Male, reloadedConfig.Knight_Male);
            Assert.Equal(config.Knight_Female, reloadedConfig.Knight_Female);
            Assert.Equal(config.Archer_Male, reloadedConfig.Archer_Male);
            Assert.Equal(config.BlackMage_Female, reloadedConfig.BlackMage_Female);
            Assert.Equal(config.Dragoon_Male, reloadedConfig.Dragoon_Male);
            Assert.Equal(config.Ninja_Female, reloadedConfig.Ninja_Female);

            // Verify JSON still has correct format (no underscores)
            Assert.Contains("\"SquireMale\":", savedJson);
            Assert.Contains("\"KnightMale\":", savedJson);
            Assert.DoesNotContain("\"Squire_Male\":", savedJson);
        }
    }
}