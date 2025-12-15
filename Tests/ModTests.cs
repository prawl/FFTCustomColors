using Xunit;
using FFTColorMod.Configuration;
using FluentAssertions;
using System.IO;
using System;
using System.Collections.Generic;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;
using FFTColorMod.Utilities;

namespace FFTColorMod.Tests
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
                var mod = new Mod(context);

                // Act & Assert - Check if mod implements the compatibility methods
                mod.CanSuspend().Should().BeFalse("Color mod should not support suspension as it modifies memory");
                mod.CanUnload().Should().BeFalse("Color mod should not support unloading due to active hooks");

                // Check that mod properly identifies itself for other mods
                var modId = mod.GetType().GetProperty("ModId")?.GetValue(mod);
                modId.Should().NotBeNull();
                modId.Should().Be("FFTColorMod");
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
                var mod = new Mod(context);

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
                var mod = new Mod(context, mockInputSimulator);
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