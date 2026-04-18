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

        // Session 35: boundary cases for StoryObjective serialization rules.

        [Fact]
        public void DetectedScreen_StoryObjectiveName_OmittedWhenNull_EvenIfIdSet()
        {
            // The name is `WhenWritingNull`, not `WhenWritingDefault`; an
            // empty string should still serialize.
            var screen = new DetectedScreen
            {
                Name = "WorldMap",
                StoryObjective = 18,
                StoryObjectiveName = null
            };

            var json = JsonSerializer.Serialize(screen);
            Assert.Contains("\"storyObjective\":18", json);
            Assert.DoesNotContain("storyObjectiveName", json);
        }

        [Fact]
        public void DetectedScreen_StoryObjectiveName_EmptyString_IsSerialized()
        {
            // `WhenWritingNull` only suppresses null — empty strings flow through.
            var screen = new DetectedScreen
            {
                Name = "WorldMap",
                StoryObjective = 5,
                StoryObjectiveName = ""
            };

            var json = JsonSerializer.Serialize(screen);
            Assert.Contains("\"storyObjectiveName\":\"\"", json);
        }

        [Fact]
        public void DetectedScreen_StoryObjectiveId1_SerializesAsNonDefault()
        {
            // Boundary: the smallest non-default int (1) must still serialize.
            var screen = new DetectedScreen
            {
                Name = "WorldMap",
                StoryObjective = 1,
                StoryObjectiveName = "Lesalia"
            };

            var json = JsonSerializer.Serialize(screen);
            Assert.Contains("\"storyObjective\":1", json);
        }

        [Fact]
        public void DetectedScreen_StoryObjective_NegativeSerializes()
        {
            // `WhenWritingDefault` on int means 0 is suppressed — negative values
            // are not default and should still appear. (No current caller sets
            // a negative, but the contract is "non-zero serializes.")
            var screen = new DetectedScreen
            {
                Name = "WorldMap",
                StoryObjective = -1,
                StoryObjectiveName = null
            };

            var json = JsonSerializer.Serialize(screen);
            Assert.Contains("\"storyObjective\":-1", json);
        }
    }
}
