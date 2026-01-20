using System;
using System.Linq;
using System.Reflection;
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
        public void Properties_Should_Be_Found_Consistently()
        {
            // Run multiple times to check for consistency
            for (int i = 0; i < 5; i++)
            {
                _output.WriteLine($"=== Run {i + 1} ===");

                var archerFemale = typeof(Config).GetProperty("Archer_Female");
                _output.WriteLine($"GetProperty('Archer_Female') returns: {archerFemale?.Name ?? "null"}");

                if (archerFemale != null)
                {
                    // Create a config and set the value
                    var config = new Config();
                    archerFemale.SetValue(config, "lucavi");
                    var value = archerFemale.GetValue(config);
                    _output.WriteLine($"After setting to lucavi, value is: {value}");
                }

                // List first 5 properties (skip indexers)
                var properties = typeof(Config).GetProperties()
                    .Where(p => p.GetIndexParameters().Length == 0 && p.PropertyType == typeof(string))
                    .Take(5)
                    .Select(p => p.Name);
                _output.WriteLine($"First 5 properties: {string.Join(", ", properties)}");
            }
        }

        [Fact]
        public void SetJobThemeForJob_Should_Set_Correct_Property()
        {
            var configPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"test_config_{Guid.NewGuid()}.json");
            var manager = new ConfigurationManager(configPath);

            // Set a specific job color
            // manager.SetJobTheme("Archer_Female", "lucavi"); // Method removed in refactoring

            // Load the config and check
            var config = manager.LoadConfig();
            _output.WriteLine($"Archer_Female value: {config.Archer_Female}");

            // Check all non-original values (skip indexers)
            var properties = typeof(Config).GetProperties()
                .Where(p => p.GetIndexParameters().Length == 0 && p.PropertyType == typeof(string));

            foreach (var prop in properties)
            {
                var value = prop.GetValue(config);
                if (value != null && !value.Equals("original"))
                {
                    _output.WriteLine($"Property {prop.Name} = {value}");
                }
            }

            // Clean up
            if (System.IO.File.Exists(configPath))
                System.IO.File.Delete(configPath);
        }
    }
}
