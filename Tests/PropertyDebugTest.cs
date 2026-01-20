using System;
using System.Linq;
using System.Reflection;
using FFTColorCustomizer.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace FFTColorCustomizer.Tests
{
    public class PropertyDebugTest
    {
        private readonly ITestOutputHelper _output;

        public PropertyDebugTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ListAllColorSchemeProperties()
        {
            var properties = typeof(Config).GetProperties()
                .Where(p => p.GetIndexParameters().Length == 0 && p.PropertyType == typeof(string))
                .OrderBy(p => p.Name)
                .ToList();

            _output.WriteLine($"Total string properties: {properties.Count}");
            _output.WriteLine("Property names:");

            foreach (var prop in properties)
            {
                _output.WriteLine($"  - {prop.Name}");
            }

            // Specific check for Archer_Female
            var archerFemale = typeof(Config).GetProperty("Archer_Female");
            _output.WriteLine($"\nGetProperty('Archer_Female') returns: {archerFemale?.Name ?? "null"}");

            // Try without underscore
            var archerFemaleNoUnderscore = typeof(Config).GetProperty("ArcherFemale");
            _output.WriteLine($"GetProperty('ArcherFemale') returns: {archerFemaleNoUnderscore?.Name ?? "null"}");
        }
    }
}
