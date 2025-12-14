using System;
using Xunit;
using FFTColorMod.Utilities;

namespace FFTColorMod.Tests
{
    public class ConfigHotkeyTests
    {
        [Fact]
        public void ProcessKey_Should_Trigger_Callback_For_C_Key()
        {
            // Arrange
            const int VK_C = 0x43; // Virtual key code for 'C'
            int capturedKey = 0;
            bool callbackTriggered = false;

            var hotkeyHandler = new HotkeyHandler((key) =>
            {
                capturedKey = key;
                callbackTriggered = true;
            });

            // Act
            hotkeyHandler.ProcessKey(VK_C);

            // Assert
            Assert.True(callbackTriggered, "Callback should be triggered for C key");
            Assert.Equal(VK_C, capturedKey);
        }
    }
}