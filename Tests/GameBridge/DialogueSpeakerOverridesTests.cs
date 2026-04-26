using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class DialogueSpeakerOverridesTests
    {
        [Fact]
        public void Get_KnownEvent045Boxes_ReturnsCorrectSpeaker()
        {
            // Anchors confirmed by user during the 2026-04-26 live walkthrough.
            Assert.Equal("Lord Dycedarg Beoulve", DialogueSpeakerOverrides.Get(45, 0));
            Assert.Equal("Delita", DialogueSpeakerOverrides.Get(45, 3));
            Assert.Equal("Ramza", DialogueSpeakerOverrides.Get(45, 7));
            Assert.Equal("Lord Dycedarg Beoulve", DialogueSpeakerOverrides.Get(45, 8));
            Assert.Equal("Lord Dycedarg Beoulve", DialogueSpeakerOverrides.Get(45, 9));
            Assert.Equal("Man's Voice", DialogueSpeakerOverrides.Get(45, 12));
            Assert.Equal("Well-dressed Man", DialogueSpeakerOverrides.Get(45, 14));
            Assert.Equal("Duke Larg", DialogueSpeakerOverrides.Get(45, 16));
        }

        [Fact]
        public void Get_UnknownEvent_ReturnsNull()
        {
            Assert.Null(DialogueSpeakerOverrides.Get(999, 0));
        }

        [Fact]
        public void Get_KnownEventOutOfBoundsBox_ReturnsNull()
        {
            // Event 45 has 29 boxes (0-28). Box 100 is past the end.
            Assert.Null(DialogueSpeakerOverrides.Get(45, 100));
        }

        [Fact]
        public void Get_NegativeBoxIndex_ReturnsNull()
        {
            Assert.Null(DialogueSpeakerOverrides.Get(45, -1));
        }

        [Fact]
        public void Get_Event045AllBoxesHaveSpeaker()
        {
            // Every one of the 29 boxes in event 045 should have an entry —
            // the whole point of the override is to fill the [narrator] gaps.
            for (int box = 0; box < 29; box++)
            {
                var speaker = DialogueSpeakerOverrides.Get(45, box);
                Assert.False(string.IsNullOrEmpty(speaker),
                    $"Event 45 box {box} has no speaker override");
            }
        }
    }
}
