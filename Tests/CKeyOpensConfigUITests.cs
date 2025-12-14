using Xunit;

namespace FFTColorMod.Tests
{
    public class CKeyOpensConfigUITests
    {
        [Fact]
        public void ProcessHotkeyPress_C_Should_Request_ConfigUI()
        {
            // Test that C key triggers the configuration UI request

            // Arrange
            const int VK_C = 0x43;
            var mod = new Mod(null, null);
            bool configUIRequested = false;

            // Subscribe to the request event
            mod.ConfigUIRequested += () => configUIRequested = true;

            // Act
            mod.ProcessHotkeyPress(VK_C);

            // Assert
            Assert.True(configUIRequested, "C key should request configuration UI");
        }
    }
}