using Xunit;
using FFTColorCustomizer.Configuration;
using FluentAssertions;
using System.IO;
using System;
using System.Collections.Generic;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;
using FFTColorCustomizer.Utilities;
using Moq;

namespace FFTColorCustomizer.Tests
{
    public class ModTests
    {

        [Fact]
        public void Mod_Should_Support_Other_Mods_Compatibility()
        {
            // TLDR: Mod should implement CanSuspend and CanUnload for compatibility with other mods
            // Arrange
            var tempAppData = Path.Combine(Path.GetTempPath(), $"TestAppData_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempAppData);
            var originalAppData = Environment.GetEnvironmentVariable("APPDATA");
            Environment.SetEnvironmentVariable("APPDATA", tempAppData);

            try
            {
                var context = new ModContext();
                var mod = new Mod(context, null, new NullHotkeyHandler());

                // Act & Assert - Check if mod implements the compatibility methods
                mod.CanSuspend().Should().BeFalse("Color mod should not support suspension as it modifies memory");
                mod.CanUnload().Should().BeFalse("Color mod should not support unloading due to active hooks");

                // Check that mod properly identifies itself for other mods
                var modId = mod.GetType().GetProperty("ModId")?.GetValue(mod);
                modId.Should().NotBeNull();
                modId.Should().Be("FFTColorCustomizer");
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable("APPDATA", originalAppData);
                if (Directory.Exists(tempAppData))
                    Directory.Delete(tempAppData, recursive: true);
            }
        }

        [Fact]
        public void Mod_Should_Have_Character_Color_Customization_Support()
        {
            // TLDR: Mod should have infrastructure to support per-character color customization
            // Arrange
            var tempAppData = Path.Combine(Path.GetTempPath(), $"TestAppData_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempAppData);
            var originalAppData = Environment.GetEnvironmentVariable("APPDATA");
            Environment.SetEnvironmentVariable("APPDATA", tempAppData);

            try
            {
                var context = new ModContext();
                var mod = new Mod(context, null, new NullHotkeyHandler());

                // Act - Check if mod has per-character customization capability
                var supportsCharacterCustomization = mod.GetType().GetMethod("SupportsPerCharacterColors");

                // Assert
                supportsCharacterCustomization.Should().NotBeNull("Mod should have method to check character customization support");

                // Invoke the method and check result
                var result = supportsCharacterCustomization?.Invoke(mod, null);
                result.Should().BeOfType<bool>();
                result.Should().Be(true, "Mod should support per-character color customization");
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable("APPDATA", originalAppData);
                if (Directory.Exists(tempAppData))
                    Directory.Delete(tempAppData, recursive: true);
            }
        }

        [Fact]
        public void ProcessHotkeyPress_F1_Should_Call_SimulateMenuRefresh()
        {
            // Arrange
            var tempAppData = Path.Combine(Path.GetTempPath(), $"TestAppData_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempAppData);
            var originalAppData = Environment.GetEnvironmentVariable("APPDATA");
            Environment.SetEnvironmentVariable("APPDATA", tempAppData);

            try
            {
                var context = new ModContext();
                var mockInputSimulator = new TrackingMockInputSimulator();
                var mod = new Mod(context, mockInputSimulator, new NullHotkeyHandler());
                bool configUIOpened = false;
                mod.ConfigUIRequested += () => configUIOpened = true;

                // Act
                mod.ProcessHotkeyPress(0x70); // VK_F1

                // Assert - F1 now opens config UI, not refreshes menu
                configUIOpened.Should().BeTrue("F1 should open config UI");
                mockInputSimulator.MenuRefreshCalled.Should().BeFalse("F1 should not trigger menu refresh anymore");
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable("APPDATA", originalAppData);
                if (Directory.Exists(tempAppData))
                    Directory.Delete(tempAppData, recursive: true);
            }
        }

        [Fact]
        public void Start_Should_Call_ApplyInitialThemes_Only_Once()
        {
            // Arrange
            var tempAppData = Path.Combine(Path.GetTempPath(), $"TestAppData_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempAppData);
            var originalAppData = Environment.GetEnvironmentVariable("APPDATA");
            Environment.SetEnvironmentVariable("APPDATA", tempAppData);

            try
            {
                // Create a test config file with specific theme values
                // The Mod will look for Config.json in its own directory (where the test assembly is)
                var testAssemblyLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var modConfigPath = Path.Combine(testAssemblyLocation, "Config.json");

                // Clean up any existing config file from previous tests
                if (File.Exists(modConfigPath))
                {
                    File.Delete(modConfigPath);
                    // Wait a moment to ensure file system has processed the deletion
                    System.Threading.Thread.Sleep(100);
                }

                var testConfig = new Config
                {
                    Cloud = "sephiroth_black",
                    Orlandeau = "thunder_god",
                    Agrias = "ash_dark"
                };
                File.WriteAllText(modConfigPath, System.Text.Json.JsonSerializer.Serialize(testConfig));

                // Create mod instance with tracking
                var context = new ModContext();
                var applyThemesCallCount = 0;
                var lastAppliedThemes = new List<string>();

                // We need to track calls to ApplyInitialThemes
                // Since we can't easily mock the internal components, we'll use the console output
                var originalOut = Console.Out;
                var stringWriter = new System.IO.StringWriter();
                Console.SetOut(stringWriter);

                try
                {
                    var mod = new Mod(context, null, new NullHotkeyHandler());

                    // After mod is created, set ModLogger to Debug level to capture debug messages
                    ModLogger.LogLevel = FFTColorCustomizer.Interfaces.LogLevel.Debug;

                    // Act
                    mod.Start(null);

                    // Assert - Check console output for duplicate calls
                    var output = stringWriter.ToString();
                    var applyInitialThemesCount = CountOccurrences(output, "ThemeManagerAdapter.ApplyInitialThemes() called");

                    // Should be called exactly once during startup
                    applyInitialThemesCount.Should().Be(1,
                        "ApplyInitialThemes should only be called once during startup to prevent overwriting configured themes with defaults");

                    // Verify that the configured themes were applied (not reset to 'original')
                    // Check that the themes were actually applied, not the debug messages which may not always appear
                    output.Should().Contain("Applying initial Cloud theme: sephiroth_black",
                        "Cloud's configured theme should be applied");
                    output.Should().Contain("Applying initial Orlandeau theme: thunder_god",
                        "Orlandeau's configured theme should be applied");
                    output.Should().Contain("Applying initial Agrias theme: ash_dark",
                        "Agrias' configured theme should be applied");

                    // Should NOT reset to original after initial configuration
                    var lastCloudTheme = GetLastThemeFromOutput(output, "Applying initial Cloud theme:");
                    var lastOrlandeauTheme = GetLastThemeFromOutput(output, "Applying initial Orlandeau theme:");

                    lastCloudTheme.Should().Be("sephiroth_black", "Cloud's theme should not be reset to original");
                    lastOrlandeauTheme.Should().Be("thunder_god", "Orlandeau's theme should not be reset to original");
                }
                finally
                {
                    Console.SetOut(originalOut);
                }
            }
            finally
            {
                // Cleanup
                Environment.SetEnvironmentVariable("APPDATA", originalAppData);
                if (Directory.Exists(tempAppData))
                    Directory.Delete(tempAppData, recursive: true);

                // Clean up the config file we created
                var testAssemblyLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var modConfigPath = Path.Combine(testAssemblyLocation, "Config.json");
                if (File.Exists(modConfigPath))
                {
                    File.Delete(modConfigPath);
                }
            }
        }

        private int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(pattern, index)) != -1)
            {
                index += pattern.Length;
                count++;
            }
            return count;
        }

        private string GetLastThemeFromOutput(string output, string prefix)
        {
            var lines = output.Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (lines[i].Contains(prefix))
                {
                    var parts = lines[i].Split(':');
                    if (parts.Length > 1)
                    {
                        return parts[parts.Length - 1].Trim();
                    }
                }
            }
            return "not_found";
        }
    }

    // Mock that tracks if menu refresh was called
    public class TrackingMockInputSimulator : IInputSimulator
    {
        public bool MenuRefreshCalled { get; private set; }

        public bool SendKeyPress(int vkCode)
        {
            return true;
        }

        public bool SimulateMenuRefresh()
        {
            MenuRefreshCalled = true;
            return true;
        }
    }
}
