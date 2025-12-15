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
        public void ProcessHotkeyPress_F1_OpensConfigUI()
        {
            // Arrange
            var mockInputSimulator = new MockInputSimulator();
            var mod = new Mod(new ModContext(), mockInputSimulator);
            bool configUIOpened = false;
            mod.ConfigUIRequested += () => configUIOpened = true;

            // Act
            mod.ProcessHotkeyPress(0x70); // F1 key

            // Assert - Should open config UI
            Assert.True(configUIOpened);
        }

    }
}