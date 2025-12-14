using System;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace FFTColorMod.Tests
{
    public class PropertyNameTest
    {
        private readonly ITestOutputHelper _output;

        public PropertyNameTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ListAllConfigProperties()
        {
            var configType = typeof(FFTColorMod.Configuration.Config);
            var properties = configType.GetProperties()
                .Where(p => p.PropertyType == typeof(FFTColorMod.Configuration.ColorScheme))
                .OrderBy(p => p.Name);

            _output.WriteLine("Config properties of type ColorScheme:");
            foreach (var prop in properties)
            {
                _output.WriteLine($"  Property Name: {prop.Name}");

                // Check for JsonPropertyName attribute
                var jsonPropName = prop.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>();
                if (jsonPropName != null)
                {
                    _output.WriteLine($"    JsonPropertyName: {jsonPropName.Name}");
                }

                // Check for JsonProperty attribute (Newtonsoft)
                var newtonsoftProp = prop.GetCustomAttribute<Newtonsoft.Json.JsonPropertyAttribute>();
                if (newtonsoftProp != null)
                {
                    _output.WriteLine($"    JsonProperty: {newtonsoftProp.PropertyName}");
                }
            }

            // Test specific property lookup
            _output.WriteLine("\nTesting specific lookups:");

            var archerFemale = configType.GetProperty("Archer_Female");
            _output.WriteLine($"GetProperty('Archer_Female'): {archerFemale?.Name ?? "NOT FOUND"}");

            var archerFemaleNoUnderscore = configType.GetProperty("ArcherFemale");
            _output.WriteLine($"GetProperty('ArcherFemale'): {archerFemaleNoUnderscore?.Name ?? "NOT FOUND"}");
        }
    }
}