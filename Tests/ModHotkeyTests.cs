using System;
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
            mod.SetColorScheme("corpse_brigade"); // Start from known position

            // Act
            mod.ProcessHotkeyPress(0x70); // F1 key

            // Assert - corpse_brigade is at index 1, so previous should be original (index 0)
            Assert.Equal("original", mod.GetCurrentColorScheme()); // Should cycle backward to original
        }

        [Fact]
        public void ProcessHotkeyPress_F2_CyclesForward()
        {
            // Arrange
            var mockInputSimulator = new MockInputSimulator();
            var mod = new Mod(new ModContext(), mockInputSimulator);
            mod.SetColorScheme("original"); // Start from known position

            // Act
            mod.ProcessHotkeyPress(0x71); // F2 key

            // Assert - original is at index 0, so next should be corpse_brigade (index 1)
            Assert.Equal("corpse_brigade", mod.GetCurrentColorScheme()); // Should cycle forward
        }

        [Fact]
        public void ProcessHotkeyPress_F1_F2_Cycle_Returns_To_Same()
        {
            // Arrange
            var mockInputSimulator = new MockInputSimulator();
            var mod = new Mod(new ModContext(), mockInputSimulator);
            mod.SetColorScheme("lucavi"); // Start from known position

            // Act - F2 then F1 should return to same position
            mod.ProcessHotkeyPress(0x71); // F2 key (forward)
            mod.ProcessHotkeyPress(0x70); // F1 key (backward)

            // Assert - should be back at lucavi
            Assert.Equal("lucavi", mod.GetCurrentColorScheme());
        }
    }
}