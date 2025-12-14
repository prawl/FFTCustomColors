using System;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace FFTColorMod.Tests
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
            var properties = typeof(Configuration.Config).GetProperties()
                .Where(p => p.PropertyType == typeof(Configuration.ColorScheme))
                .OrderBy(p => p.Name)
                .ToList();

            _output.WriteLine($"Total ColorScheme properties: {properties.Count}");
            _output.WriteLine("Property names:");

            foreach (var prop in properties)
            {
                _output.WriteLine($"  - {prop.Name}");
            }

            // Specific check for Archer_Female
            var archerFemale = typeof(Configuration.Config).GetProperty("Archer_Female");
            _output.WriteLine($"\nGetProperty('Archer_Female') returns: {archerFemale?.Name ?? "null"}");

            // Try without underscore
            var archerFemaleNoUnderscore = typeof(Configuration.Config).GetProperty("ArcherFemale");
            _output.WriteLine($"GetProperty('ArcherFemale') returns: {archerFemaleNoUnderscore?.Name ?? "null"}");
        }
    }
}