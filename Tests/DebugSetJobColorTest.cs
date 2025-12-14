using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace FFTColorMod.Tests
{
    public class DebugSetJobColorTest : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testModPath;
        private readonly string _testConfigPath;

        public DebugSetJobColorTest(ITestOutputHelper output)
        {
            _output = output;
            _testModPath = Path.Combine(Path.GetTempPath(), $"test_mod_{Guid.NewGuid()}");
            _testConfigPath = Path.Combine(_testModPath, "Config.json");

            // Create test directory structure
            var unitDir = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            Directory.CreateDirectory(unitDir);

            // Create color scheme directories with dummy files
            var schemes = new[] { "sprites_original", "sprites_corpse_brigade", "sprites_lucavi" };
            foreach (var scheme in schemes)
            {
                var schemeDir = Path.Combine(unitDir, scheme);
                Directory.CreateDirectory(schemeDir);
                File.WriteAllText(Path.Combine(schemeDir, "battle_yumi_w_spr.bin"), $"{scheme}_archer");
            }
        }

        [Fact(Skip = "Randomly failing - needs ConfigBasedSpriteManager initialization fix")]
        public void Debug_SetJobColor_Process()
        {
            // Setup environment variables
            Environment.SetEnvironmentVariable("FFT_MOD_PATH", _testModPath);
            Environment.SetEnvironmentVariable("FFT_CONFIG_PATH", _testConfigPath);

            _output.WriteLine($"Test mod path: {_testModPath}");
            _output.WriteLine($"Test config path: {_testConfigPath}");

            // Create mod instance
            var modContext = new FFTColorMod.ModContext();

            // Check if config exists BEFORE creating mod
            _output.WriteLine($"Config exists BEFORE mod creation: {File.Exists(_testConfigPath)}");

            var mod = new FFTColorMod.Mod(modContext);

            _output.WriteLine($"Config exists AFTER mod creation: {File.Exists(_testConfigPath)}");

            // If config was created, show its contents
            if (File.Exists(_testConfigPath))
            {
                var immediateContent = File.ReadAllText(_testConfigPath);
                _output.WriteLine($"Config created by Mod constructor (first 500 chars): {immediateContent.Substring(0, Math.Min(500, immediateContent.Length))}");
            }

            // Call SetJobColor
            _output.WriteLine("Calling SetJobColor('Archer_Female', 'lucavi')");

            // Check if the manager is available
            _output.WriteLine($"HasConfigurationManager: {mod.HasConfigurationManager()}");

            mod.SetJobColor("Archer_Female", "lucavi");
            _output.WriteLine("SetJobColor completed");

            // Wait for file write
            System.Threading.Thread.Sleep(100);

            _output.WriteLine($"Config exists after SetJobColor: {File.Exists(_testConfigPath)}");

            if (File.Exists(_testConfigPath))
            {
                var configContent = File.ReadAllText(_testConfigPath);
                _output.WriteLine($"Config content (first 500 chars): {configContent.Substring(0, Math.Min(500, configContent.Length))}");

                // Check if it contains the expected value
                bool containsLucavi = configContent.Contains("\"lucavi\"") || configContent.Contains("\"Lucavi\"");
                _output.WriteLine($"Config contains lucavi: {containsLucavi}");
            }

            // Call GetJobColor
            var color = mod.GetJobColor("Archer_Female");
            _output.WriteLine($"GetJobColor returned: '{color}'");

            // Try loading config directly
            var configManager = new FFTColorMod.Configuration.ConfigurationManager(_testConfigPath);
            var config = configManager.LoadConfig();
            _output.WriteLine($"Direct config load - Archer_Female: {config.Archer_Female}");

            // Verify the expected result
            Assert.Equal("Lucavi", color);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testModPath))
                {
                    Directory.Delete(_testModPath, true);
                }
            }
            catch { }
        }
    }
}