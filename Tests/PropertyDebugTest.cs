using System;
using System.Linq;
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
        public void ListAllJobKeys()
        {
            var config = new Config();
            var jobKeys = config.GetAllJobKeys().OrderBy(k => k).ToList();

            _output.WriteLine($"Total job keys: {jobKeys.Count}");
            _output.WriteLine("Job key names:");

            foreach (var key in jobKeys)
            {
                _output.WriteLine($"  - {key}");
            }

            // Specific check for Archer_Female
            var hasArcherFemale = jobKeys.Contains("Archer_Female");
            _output.WriteLine($"\nContains 'Archer_Female': {hasArcherFemale}");

            // Check indexer access
            var archerFemaleValue = config["Archer_Female"];
            _output.WriteLine($"config[\"Archer_Female\"] returns: {archerFemaleValue}");
        }
    }
}
