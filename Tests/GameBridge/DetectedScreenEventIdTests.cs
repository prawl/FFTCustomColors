using System.Text.Json;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class DetectedScreenEventIdTests
    {
        [Fact]
        public void DetectedScreen_EventId_SerializesWhenNonZero()
        {
            var screen = new DetectedScreen
            {
                Name = "Cutscene",
                EventId = 10
            };

            var json = JsonSerializer.Serialize(screen);
            Assert.Contains("\"eventId\":10", json);
        }

        [Fact]
        public void DetectedScreen_EventId_OmittedWhenZero()
        {
            var screen = new DetectedScreen
            {
                Name = "WorldMap",
                EventId = 0
            };

            var json = JsonSerializer.Serialize(screen);
            Assert.DoesNotContain("eventId", json);
        }
    }
}
