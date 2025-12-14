using System;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace FFTColorMod.Tests
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

                var archerFemale = typeof(Configuration.Config).GetProperty("Archer_Female");
                _output.WriteLine($"GetProperty('Archer_Female') returns: {archerFemale?.Name ?? "null"}");

                if (archerFemale != null)
                {
                    // Create a config and set the value
                    var config = new Configuration.Config();
                    archerFemale.SetValue(config, Configuration.ColorScheme.lucavi);
                    var value = archerFemale.GetValue(config);
                    _output.WriteLine($"After setting to lucavi, value is: {value}");
                }

                // List first 5 properties
                var properties = typeof(Configuration.Config).GetProperties()
                    .Where(p => p.PropertyType == typeof(Configuration.ColorScheme))
                    .Take(5)
                    .Select(p => p.Name);
                _output.WriteLine($"First 5 properties: {string.Join(", ", properties)}");
            }
        }

        [Fact]
        public void SetColorSchemeForJob_Should_Set_Correct_Property()
        {
            var configPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"test_config_{Guid.NewGuid()}.json");
            var manager = new Configuration.ConfigurationManager(configPath);

            // Set a specific job color
            manager.SetColorSchemeForJob("Archer_Female", "lucavi");

            // Load the config and check
            var config = manager.LoadConfig();
            _output.WriteLine($"Archer_Female value: {config.Archer_Female}");

            // Check all non-original values
            var properties = typeof(Configuration.Config).GetProperties()
                .Where(p => p.PropertyType == typeof(Configuration.ColorScheme));

            foreach (var prop in properties)
            {
                var value = prop.GetValue(config);
                if (value != null && !value.Equals(Configuration.ColorScheme.original))
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