using Xunit;
using FFTColorMod.Utilities;

namespace FFTColorMod.Tests
{
    public class CKeyMonitoringTests
    {
        [Fact]
        public void HotkeyHandler_Should_Include_C_Key_In_Monitoring()
        {
            // Arrange
            const int VK_C = 0x43;

            // Act
            var isCKeyDefined = HotkeyHandler.IsKeyMonitored(VK_C);

            // Assert
            Assert.True(isCKeyDefined, "C key should be included in monitored keys");
        }
    }
}