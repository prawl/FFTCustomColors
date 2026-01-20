using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Xunit;
using Xunit.Abstractions;
using ConfigurationNamespace = FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Tests
{
    public class SimplifiedPropertySetTest : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testPath;
        private static int _testCounter = 0;

        public SimplifiedPropertySetTest(ITestOutputHelper output)
        {
            _output = output;
            var testId = Interlocked.Increment(ref _testCounter);
            _testPath = Path.Combine(Path.GetTempPath(), $"test_simple_{testId}_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testPath);
        }

        [Fact]
        public void Test_Direct_Property_Setting()
        {
            _output.WriteLine("=== Direct Dictionary-Based Property Setting Test ===\n");

            // Create a config instance
            var config = new ConfigurationNamespace.Config();

            // Get all job keys using dictionary-based access
            var jobKeys = config.GetAllJobKeys().OrderBy(k => k).ToList();

            _output.WriteLine($"Total job keys: {jobKeys.Count}");
            _output.WriteLine($"First 5 job keys: {string.Join(", ", jobKeys.Take(5))}\n");

            // Test setting Archer_Female directly using indexer
            _output.WriteLine($"Setting Archer_Female via indexer...");

            // Set value using indexer
            config["Archer_Female"] = "lucavi";

            // Get value back
            var getValue = config["Archer_Female"];
            _output.WriteLine($"After SetValue - GetValue result: {getValue}");
            _output.WriteLine($"Direct access - config[\"Archer_Female\"]: {config["Archer_Female"]}");

            // Check other properties to see if they were affected
            _output.WriteLine($"\nChecking other properties:");
            _output.WriteLine($"config[\"Squire_Male\"]: {config["Squire_Male"]}");
            _output.WriteLine($"config[\"Knight_Male\"]: {config["Knight_Male"]}");

            Assert.Equal("lucavi", config["Archer_Female"]);
            Assert.Equal("original", config["Squire_Male"]);
        }

        [Fact]
        public void Test_ConfigurationManager_SetJobTheme()
        {
            _output.WriteLine("=== ConfigurationManager Property Setting Test ===\n");

            var configPath = Path.Combine(_testPath, "Config.json");
            var manager = new ConfigurationNamespace.ConfigurationManager(configPath);

            // Load config and manually set property
            var config = manager.LoadConfig();
            config["Archer_Female"] = "lucavi";

            // Save the updated config
            manager.SaveConfig(config);
            _output.WriteLine("Set Archer_Female to 'lucavi' and saved config");

            // Reload config to verify persistence
            var reloadedConfig = manager.LoadConfig();
            _output.WriteLine($"\nAfter setting:");
            _output.WriteLine($"config[\"Archer_Female\"]: {reloadedConfig["Archer_Female"]}");
            _output.WriteLine($"config[\"Squire_Male\"]: {reloadedConfig["Squire_Male"]}");
            _output.WriteLine($"config[\"Knight_Male\"]: {reloadedConfig["Knight_Male"]}");

            // Read the JSON file directly
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                _output.WriteLine($"\nRaw JSON (first 300 chars):");
                _output.WriteLine(json.Substring(0, Math.Min(300, json.Length)));
            }

            Assert.Equal("lucavi", reloadedConfig["Archer_Female"]);
            Assert.Equal("original", reloadedConfig["Squire_Male"]); // Should remain default
        }

        [Fact]
        public void Test_Job_Key_Order()
        {
            _output.WriteLine("=== Testing Job Key Order ===\n");

            var config = new ConfigurationNamespace.Config();
            var jobKeys = config.GetAllJobKeys().ToList();

            // Get job keys in different orders
            var orderedByName = jobKeys.OrderBy(k => k).ToList();

            _output.WriteLine("First 10 job keys (by name):");
            foreach (var key in orderedByName.Take(10))
            {
                _output.WriteLine($"  {key}");
            }

            _output.WriteLine("\nFirst 10 job keys (dictionary order):");
            foreach (var key in jobKeys.Take(10))
            {
                _output.WriteLine($"  {key}");
            }

            // Find index of Archer_Female in both
            var indexByName = orderedByName.IndexOf("Archer_Female");
            var indexDict = jobKeys.IndexOf("Archer_Female");

            _output.WriteLine($"\nArcher_Female index (by name): {indexByName}");
            _output.WriteLine($"Archer_Female index (dictionary): {indexDict}");

            // What job key is at index 0?
            if (orderedByName.Count > 0)
            {
                _output.WriteLine($"\nJob key at index 0 (by name): {orderedByName[0]}");
            }
            if (jobKeys.Count > 0)
            {
                _output.WriteLine($"Job key at index 0 (dictionary): {jobKeys[0]}");
            }

            // Verify Archer_Female exists
            Assert.Contains("Archer_Female", jobKeys);
        }

        [Fact]
        public void Test_Multiple_Rapid_Sets()
        {
            _output.WriteLine("=== Testing Multiple Rapid Sets ===\n");

            var configPath = Path.Combine(_testPath, "Config.json");
            var manager = new ConfigurationNamespace.ConfigurationManager(configPath);

            // Load config and set multiple properties
            var config = manager.LoadConfig();

            // Perform multiple rapid sets
            _output.WriteLine("Setting multiple properties rapidly:");
            config["Knight_Male"] = "corpse_brigade";
            config["Archer_Female"] = "lucavi";
            config["Monk_Male"] = "northern_sky";

            // Save all changes at once
            manager.SaveConfig(config);

            // Reload and check
            var reloadedConfig = manager.LoadConfig();
            _output.WriteLine($"\nResults:");
            _output.WriteLine($"Knight_Male: {reloadedConfig["Knight_Male"]} (expected: corpse_brigade)");
            _output.WriteLine($"Archer_Female: {reloadedConfig["Archer_Female"]} (expected: lucavi)");
            _output.WriteLine($"Monk_Male: {reloadedConfig["Monk_Male"]} (expected: northern_sky)");

            Assert.Equal("corpse_brigade", reloadedConfig["Knight_Male"]);
            Assert.Equal("lucavi", reloadedConfig["Archer_Female"]);
            Assert.Equal("northern_sky", reloadedConfig["Monk_Male"]);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testPath))
                {
                    Directory.Delete(_testPath, true);
                }
            }
            catch { }
        }
    }
}
