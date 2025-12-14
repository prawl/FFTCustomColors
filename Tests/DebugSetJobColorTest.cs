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
            // Use a more unique path to avoid any cross-test contamination
            var uniqueId = Guid.NewGuid();
            _testModPath = Path.Combine(Path.GetTempPath(), $"debug_setjob_test_{uniqueId}");
            _testConfigPath = Path.Combine(_testModPath, $"Config_{uniqueId}.json");

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

        [Fact]
        public void Debug_SetJobColor_Process()
        {
            // Use the simpler ConfigurationManager_DirectTest approach for now
            // The Mod class seems to have static state issues
            _output.WriteLine($"Test config path: {_testConfigPath}");

            // Create managers directly without going through Mod
            var configManager = new FFTColorMod.Configuration.ConfigurationManager(_testConfigPath);
            var configBasedSpriteManager = new FFTColorMod.Utilities.ConfigBasedSpriteManager(
                _testModPath, configManager, null);

            // Set a job color
            configBasedSpriteManager.SetColorForJob("Archer_Female", "lucavi");

            // Get the color
            var color = configBasedSpriteManager.GetActiveColorForJob("Archer_Female");
            _output.WriteLine($"GetActiveColorForJob returned: '{color}'");

            // Try loading config directly with a fresh ConfigurationManager to verify persistence
            _output.WriteLine($"Creating fresh ConfigurationManager with path: {_testConfigPath}");
            var freshConfigManager = new FFTColorMod.Configuration.ConfigurationManager(_testConfigPath);
            var diskConfig = freshConfigManager.LoadConfig();
            _output.WriteLine($"Direct config load from disk - Archer_Female enum value: {diskConfig.Archer_Female}");
            _output.WriteLine($"Direct config load from disk - Archer_Female int value: {(int)diskConfig.Archer_Female}");

            // Check the raw file content
            if (File.Exists(_testConfigPath))
            {
                var content = File.ReadAllText(_testConfigPath);
                _output.WriteLine($"Raw config file (first 500 chars): {content.Substring(0, Math.Min(500, content.Length))}");
            }

            // Verify the config was actually persisted to disk with the correct value
            Assert.Equal(FFTColorMod.Configuration.ColorScheme.lucavi, diskConfig.Archer_Female);

            // Verify the GetJobColor method returns the correct display name
            Assert.Equal("Lucavi", color);
        }

        [Fact]
        public void ConfigurationManager_DirectTest()
        {
            _output.WriteLine($"Test config path: {_testConfigPath}");

            // Create a ConfigurationManager directly
            var configManager = new FFTColorMod.Configuration.ConfigurationManager(_testConfigPath);

            // Set a job color
            configManager.SetColorSchemeForJob("Archer_Female", "lucavi");

            // Load the config with a fresh manager
            var freshManager = new FFTColorMod.Configuration.ConfigurationManager(_testConfigPath);
            var config = freshManager.LoadConfig();

            _output.WriteLine($"Archer_Female value: {config.Archer_Female}");
            _output.WriteLine($"Archer_Female int: {(int)config.Archer_Female}");

            // Check the raw file content
            if (File.Exists(_testConfigPath))
            {
                var content = File.ReadAllText(_testConfigPath);
                _output.WriteLine($"Raw config file (first 500 chars): {content.Substring(0, Math.Min(500, content.Length))}");
            }

            Assert.Equal(FFTColorMod.Configuration.ColorScheme.lucavi, config.Archer_Female);
        }

        public void Dispose()
        {
            // Add a small delay to ensure file handles are released
            System.Threading.Thread.Sleep(100);

            // Force garbage collection to ensure any finalizers run
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            if (Directory.Exists(_testModPath))
            {
                try
                {
                    // Try to delete all files first with retry logic
                    foreach (var file in Directory.GetFiles(_testModPath, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            File.SetAttributes(file, FileAttributes.Normal);
                            File.Delete(file);
                        }
                        catch
                        {
                            // If we can't delete a file, try again after a short delay
                            System.Threading.Thread.Sleep(50);
                            try
                            {
                                File.Delete(file);
                            }
                            catch
                            {
                                // Ignore - best effort cleanup
                            }
                        }
                    }

                    // Now try to delete the directory
                    Directory.Delete(_testModPath, true);
                }
                catch (IOException)
                {
                    // Ignore cleanup errors in tests - this is best effort
                    // The temp directory will be cleaned up eventually by the OS
                }
            }
        }
    }
}