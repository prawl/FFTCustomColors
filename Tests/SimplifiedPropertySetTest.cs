using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Xunit;
using Xunit.Abstractions;
using ConfigurationNamespace = FFTColorMod.Configuration;

namespace FFTColorMod.Tests
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
            _output.WriteLine("=== Direct Property Setting Test ===\n");

            // Create a config instance
            var config = new ConfigurationNamespace.Config();

            // Get all ColorScheme properties
            var properties = typeof(ConfigurationNamespace.Config).GetProperties()
                .Where(p => p.PropertyType == typeof(FFTColorMod.Configuration.ColorScheme))
                .OrderBy(p => p.Name)
                .ToList();

            _output.WriteLine($"Total properties: {properties.Count}");
            _output.WriteLine($"First 5 properties: {string.Join(", ", properties.Take(5).Select(p => p.Name))}\n");

            // Test setting Archer_Female directly
            var archerProp = typeof(ConfigurationNamespace.Config).GetProperty("Archer_Female");
            _output.WriteLine($"GetProperty('Archer_Female'): {archerProp?.Name ?? "NULL"}");

            if (archerProp != null)
            {
                _output.WriteLine($"Property type: {archerProp.PropertyType}");
                _output.WriteLine($"Can write: {archerProp.CanWrite}");

                // Set value
                archerProp.SetValue(config, (FFTColorMod.Configuration.ColorScheme)2); // lucavi is enum value 2

                // Get value back
                var getValue = archerProp.GetValue(config);
                _output.WriteLine($"After SetValue - GetValue result: {getValue}");
                _output.WriteLine($"Direct access - config.Archer_Female: {config.Archer_Female}");

                // Check other properties to see if they were affected
                _output.WriteLine($"\nChecking other properties:");
                _output.WriteLine($"config.Squire_Male: {config.Squire_Male}");
                _output.WriteLine($"config.Knight_Male: {config.Knight_Male}");

                Assert.True(config.Archer_Female == (FFTColorMod.Configuration.ColorScheme)2); // lucavi
                Assert.True(config.Squire_Male == (FFTColorMod.Configuration.ColorScheme)0); // original
            }
        }

        [Fact]
        public void Test_ConfigurationManager_SetColorSchemeForJob()
        {
            _output.WriteLine("=== ConfigurationManager SetColorSchemeForJob Test ===\n");

            var configPath = Path.Combine(_testPath, "Config.json");
            var manager = new ConfigurationNamespace.ConfigurationManager(configPath);

            // Set Archer_Female to lucavi
            _output.WriteLine("Calling SetColorSchemeForJob('Archer_Female', 'lucavi')");
            manager.SetColorSchemeForJob("Archer_Female", "lucavi");

            // Load config and check
            var config = manager.LoadConfig();
            _output.WriteLine($"\nAfter setting:");
            _output.WriteLine($"config.Archer_Female: {config.Archer_Female}");
            _output.WriteLine($"config.Squire_Male: {config.Squire_Male}");
            _output.WriteLine($"config.Knight_Male: {config.Knight_Male}");

            // Read the JSON file directly
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                _output.WriteLine($"\nRaw JSON (first 300 chars):");
                _output.WriteLine(json.Substring(0, Math.Min(300, json.Length)));
            }

            Assert.True(config.Archer_Female == (FFTColorMod.Configuration.ColorScheme)2); // lucavi
        }

        [Fact]
        public void Test_Property_Order_Issue()
        {
            _output.WriteLine("=== Testing Property Order Issue ===\n");

            var configType = typeof(ConfigurationNamespace.Config);
            var properties = configType.GetProperties()
                .Where(p => p.PropertyType == typeof(FFTColorMod.Configuration.ColorScheme))
                .ToList();

            // Get properties in different orders
            var orderedByName = properties.OrderBy(p => p.Name).ToList();
            var declaredOrder = properties.ToList();

            _output.WriteLine("First 10 properties (by name):");
            foreach (var prop in orderedByName.Take(10))
            {
                _output.WriteLine($"  {prop.Name}");
            }

            _output.WriteLine("\nFirst 10 properties (declaration order):");
            foreach (var prop in declaredOrder.Take(10))
            {
                _output.WriteLine($"  {prop.Name}");
            }

            // Find index of Archer_Female in both
            var indexByName = orderedByName.FindIndex(p => p.Name == "Archer_Female");
            var indexDeclared = declaredOrder.FindIndex(p => p.Name == "Archer_Female");

            _output.WriteLine($"\nArcher_Female index (by name): {indexByName}");
            _output.WriteLine($"Archer_Female index (declared): {indexDeclared}");

            // What property is at index 0?
            if (orderedByName.Count > 0)
            {
                _output.WriteLine($"\nProperty at index 0 (by name): {orderedByName[0].Name}");
            }
            if (declaredOrder.Count > 0)
            {
                _output.WriteLine($"Property at index 0 (declared): {declaredOrder[0].Name}");
            }
        }

        [Fact]
        public void Test_Multiple_Rapid_Sets()
        {
            _output.WriteLine("=== Testing Multiple Rapid Sets ===\n");

            var configPath = Path.Combine(_testPath, "Config.json");
            var manager = new ConfigurationNamespace.ConfigurationManager(configPath);

            // Perform multiple rapid sets
            _output.WriteLine("Setting multiple properties rapidly:");
            manager.SetColorSchemeForJob("Knight_Male", "corpse_brigade");
            manager.SetColorSchemeForJob("Archer_Female", "lucavi");
            manager.SetColorSchemeForJob("Monk_Male", "northern_sky");

            // Load and check
            var config = manager.LoadConfig();
            _output.WriteLine($"\nResults:");
            _output.WriteLine($"Knight_Male: {config.Knight_Male} (expected: corpse_brigade)");
            _output.WriteLine($"Archer_Female: {config.Archer_Female} (expected: lucavi)");
            _output.WriteLine($"Monk_Male: {config.Monk_Male} (expected: northern_sky)");

            Assert.True(config.Knight_Male == (FFTColorMod.Configuration.ColorScheme)1); // corpse_brigade
            Assert.True(config.Archer_Female == (FFTColorMod.Configuration.ColorScheme)2); // lucavi
            Assert.True(config.Monk_Male == (FFTColorMod.Configuration.ColorScheme)3); // northern_sky
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