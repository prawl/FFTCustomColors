using System.IO;
using System.Text.Json;
using FFTColorCustomizer.Configuration;
using Xunit;

namespace FFTColorCustomizer.Tests
{
    public class StartupOnConfigurationUpdatedTests
    {
        [Fact]
        public void OnConfigurationUpdated_ShouldUseMergeLogic_NotDirectReplacement()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            // Simulate the Reloaded directory structure
            var modDir = Path.Combine(tempDir, "Mods", "FFTColorCustomizer");
            var userDir = Path.Combine(tempDir, "User", "Mods", "paxtrick.fft.colorcustomizer");
            Directory.CreateDirectory(modDir);
            Directory.CreateDirectory(userDir);

            var configPath = Path.Combine(userDir, "Config.json");

            // Create existing config with multiple settings
            var existingConfig = new Config
            {
                ["Squire_Male"] = "corpse_brigade",
                ["Knight_Female"] = "lucavi",
                ["Archer_Male"] = "northern_sky"
            };
            var existingJson = JsonSerializer.Serialize(existingConfig, Configurable<Config>.SerializerOptions);
            File.WriteAllText(configPath, existingJson);

            try
            {
                // Set up a test assembly location to simulate the mod directory
                var startup = new TestableStartup(modDir);

                // Simulate Reloaded-II sending an update with only one changed value
                var incomingConfig = new Config
                {
                    ["Squire_Male"] = "original"
                    // All other values are defaults
                };

                // Act
                startup.TestOnConfigurationUpdated(incomingConfig);

                // Assert - Read the saved config and verify all values are preserved
                var savedJson = File.ReadAllText(configPath);
                var savedConfig = JsonSerializer.Deserialize<Config>(savedJson, Configurable<Config>.SerializerOptions);

                Assert.Equal("corpse_brigade", savedConfig["Squire_Male"]); // Preserved since incoming is default
                Assert.Equal("lucavi", savedConfig["Knight_Female"]); // Preserved
                Assert.Equal("northern_sky", savedConfig["Archer_Male"]); // Preserved
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }

    // Testable version of Startup that exposes OnConfigurationUpdated
    public class TestableStartup
    {
        private readonly string _modDirectory;

        public TestableStartup(string modDirectory)
        {
            _modDirectory = modDirectory;
        }

        public void TestOnConfigurationUpdated(Config config)
        {
            var modPath = _modDirectory;
            var reloadedRoot = Path.GetDirectoryName(Path.GetDirectoryName(modPath));
            var userConfigDir = Path.Combine(reloadedRoot ?? "", "User", "Mods", "paxtrick.fft.colorcustomizer");
            var userConfigPath = Path.Combine(userConfigDir, "Config.json");

            // Use ConfigurationUpdater to handle merge logic
            var updater = new ConfigurationUpdater();
            updater.UpdateAndSaveConfiguration(config, userConfigPath);
        }
    }
}
