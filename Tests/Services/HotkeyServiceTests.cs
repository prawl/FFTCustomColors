using System;
using FFTColorMod.Configuration;
using Xunit;
using FFTColorMod;
using FFTColorMod.Utilities;

namespace FFTColorMod.Tests
{
    public class ModHotkeyTests
    {
        [Fact]
        public void ProcessHotkeyPress_F1_CyclesBackward()
        {
            // Arrange
            var mockInputSimulator = new MockInputSimulator();
            var mod = new Mod(new ModContext(), mockInputSimulator);
            var startingScheme = "corpse_brigade"; // Start from known position
            mod.SetColorScheme(startingScheme);

            // Act
            mod.ProcessHotkeyPress(0x70); // F1 key

            // Assert - Should cycle backward to a different scheme
            var currentScheme = mod.GetCurrentColorScheme();
            Assert.NotEqual(startingScheme, currentScheme); // Should have changed
            Assert.NotNull(currentScheme); // Should have a valid scheme
        }

        [Fact]
        public void ProcessHotkeyPress_F2_CyclesForward()
        {
            // Arrange
            var mockInputSimulator = new MockInputSimulator();
            var mod = new Mod(new ModContext(), mockInputSimulator);
            var startingScheme = "original"; // Start from known position
            mod.SetColorScheme(startingScheme);

            // Act
            mod.ProcessHotkeyPress(0x71); // F2 key

            // Assert - Should cycle forward to a different scheme
            var currentScheme = mod.GetCurrentColorScheme();
            Assert.NotEqual(startingScheme, currentScheme); // Should have changed
            Assert.NotEqual("original", currentScheme); // Should not be original
        }

        [Fact]
        public void ProcessHotkeyPress_F1_F2_Cycle_Returns_To_Same()
        {
            // Arrange
            var mockInputSimulator = new MockInputSimulator();
            var mod = new Mod(new ModContext(), mockInputSimulator);
            var startingScheme = "corpse_brigade"; // Start from known deployed theme
            mod.SetColorScheme(startingScheme);

            // Act - F2 then F1 should return to same position
            mod.ProcessHotkeyPress(0x71); // F2 key (forward)
            mod.ProcessHotkeyPress(0x70); // F1 key (backward)

            // Assert - should be back at starting position
            Assert.Equal(startingScheme, mod.GetCurrentColorScheme());
        }
    }
}