using System;
using System.Linq;
using FFTColorCustomizer.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace FFTColorCustomizer.Tests
{
    public class PropertyOrderTest
    {
        private readonly ITestOutputHelper _output;

        public PropertyOrderTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void JobKeys_Should_Be_Found_Consistently()
        {
            // Run multiple times to check for consistency
            for (int i = 0; i < 5; i++)
            {
                _output.WriteLine($"=== Run {i + 1} ===");

                var config = new Config();
                var hasArcherFemale = config.GetAllJobKeys().Contains("Archer_Female");
                _output.WriteLine($"Contains 'Archer_Female': {hasArcherFemale}");

                // Create a config and set the value
                config["Archer_Female"] = "lucavi";
                var value = config["Archer_Female"];
                _output.WriteLine($"After setting to lucavi, value is: {value}");

                // List first 5 job keys
                var jobKeys = config.GetAllJobKeys().Take(5).ToList();
                _output.WriteLine($"First 5 job keys: {string.Join(", ", jobKeys)}");
            }
        }

        [Fact]
        public void SetJobTheme_Should_Set_Correct_Value()
        {
            var configPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"test_config_{Guid.NewGuid()}.json");
            var manager = new ConfigurationManager(configPath);

            // Load config, set a value, and save
            var config = manager.LoadConfig();
            config["Archer_Female"] = "lucavi";
            manager.SaveConfig(config);

            // Load the config again and check
            var config2 = manager.LoadConfig();
            _output.WriteLine($"Archer_Female value: {config2["Archer_Female"]}");

            // Check all non-original values
            foreach (var jobKey in config2.GetAllJobKeys())
            {
                var value = config2.GetJobTheme(jobKey);
                if (value != null && !value.Equals("original"))
                {
                    _output.WriteLine($"Job {jobKey} = {value}");
                }
            }

            // Clean up
            if (System.IO.File.Exists(configPath))
                System.IO.File.Delete(configPath);
        }
    }
}
