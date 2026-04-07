using System.Text.Json;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class StoryObjectiveTests
    {
        [Fact]
        public void DetectedScreen_StoryObjective_SerializesWhenNonZero()
        {
            var screen = new DetectedScreen
            {
                Name = "WorldMap",
                StoryObjective = 18,
                StoryObjectiveName = "Orbonne Monastery"
            };

            var json = JsonSerializer.Serialize(screen);
            Assert.Contains("\"storyObjective\":18", json);
            Assert.Contains("\"storyObjectiveName\":\"Orbonne Monastery\"", json);
        }

        [Fact]
        public void DetectedScreen_StoryObjective_OmittedWhenZero()
        {
            var screen = new DetectedScreen
            {
                Name = "WorldMap",
                StoryObjective = 0,
                StoryObjectiveName = null
            };

            var json = JsonSerializer.Serialize(screen);
            Assert.DoesNotContain("storyObjective", json);
            Assert.DoesNotContain("storyObjectiveName", json);
        }
    }
}
