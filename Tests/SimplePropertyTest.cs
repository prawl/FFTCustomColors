using System;
using System.Linq;
using Xunit;

namespace FFTColorCustomizer.Tests
{
    public class SimplePropertyTest
    {
        [Fact]
        public void TestPropertyLookup()
        {
            // Create a config instance
            var config = new FFTColorCustomizer.Configuration.Config();

            // Test that Archer_Female exists as a job key
            var jobKeys = config.GetAllJobKeys().ToList();
            Assert.Contains("Archer_Female", jobKeys);

            // Verify the default value
            var defaultValue = config.GetJobTheme("Archer_Female");
            Assert.Equal("original", defaultValue);

            // Set using indexer
            config["Archer_Female"] = "lucavi";

            // Verify it was set
            Assert.Equal("lucavi", config["Archer_Female"]);
            Assert.Equal("lucavi", config.GetJobTheme("Archer_Female"));

            // Test the ConfigurationManager can work with configs
            var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"test_config_{Guid.NewGuid()}.json");
            var configManager = new FFTColorCustomizer.Configuration.ConfigurationManager(tempPath);

            // Load default config and set a value
            var loadedConfig = configManager.LoadConfig();
            loadedConfig["Archer_Female"] = "lucavi";
            configManager.SaveConfig(loadedConfig);

            // Reload and verify
            var reloadedConfig = configManager.LoadConfig();
            Assert.Equal("lucavi", reloadedConfig["Archer_Female"]);

            // Cleanup
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }
}
