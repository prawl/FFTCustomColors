using Xunit;

namespace FFTColorMod.Tests
{
    public class ModCKeyIntegrationTests
    {
        [Fact]
        public void ProcessHotkeyPress_Should_Handle_C_Key()
        {
            // Test that when C key (0x43) is pressed, the mod handles it

            // Arrange
            const int VK_C = 0x43;
            var mod = new Mod(null, null);
            bool configUITriggered = false;

            // Set up a way to detect if config UI was triggered
            mod.ConfigUIRequested += () => configUITriggered = true;

            // Act
            mod.ProcessHotkeyPress(VK_C);

            // Assert
            Assert.True(configUITriggered, "C key should trigger configuration UI");
        }
    }
}