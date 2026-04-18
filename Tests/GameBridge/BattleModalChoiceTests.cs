using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pure scaffolding for BattleObjectiveChoice / RecruitOffer modal states.
    /// Detection-level work (finding the memory byte that discriminates these
    /// modals from BattleDialogue) is deferred — this just locks the option-
    /// label lookup and ValidPaths list so downstream code compiles with stable
    /// API once detection lands.
    /// </summary>
    public class BattleModalChoiceTests
    {
        [Fact]
        public void GetHighlightedLabel_Cursor0_ReturnsOptionA()
        {
            Assert.Equal("Save Agrias",
                BattleModalChoice.GetHighlightedLabel(0, "Save Agrias", "Defeat all enemies"));
        }

        [Fact]
        public void GetHighlightedLabel_Cursor1_ReturnsOptionB()
        {
            Assert.Equal("Defeat all enemies",
                BattleModalChoice.GetHighlightedLabel(1, "Save Agrias", "Defeat all enemies"));
        }

        [Fact]
        public void GetHighlightedLabel_NegativeCursor_ReturnsNull()
        {
            Assert.Null(BattleModalChoice.GetHighlightedLabel(-1, "A", "B"));
        }

        [Fact]
        public void GetHighlightedLabel_CursorOutOfRange_ReturnsNull()
        {
            Assert.Null(BattleModalChoice.GetHighlightedLabel(2, "A", "B"));
        }

        [Fact]
        public void GetHighlightedLabel_MissingOption_ReturnsNull()
        {
            // Caller passed null for the label at the cursor index — degrade
            // gracefully so the screen render doesn't crash.
            Assert.Null(BattleModalChoice.GetHighlightedLabel(0, null, "B"));
            Assert.Null(BattleModalChoice.GetHighlightedLabel(1, "A", null));
            Assert.Null(BattleModalChoice.GetHighlightedLabel(0, "", "B"));
        }

        [Fact]
        public void ValidPathNames_HasExactly4Entries()
        {
            var names = BattleModalChoice.ValidPathNames;
            Assert.Equal(4, names.Count);
            Assert.Contains("CursorUp", names);
            Assert.Contains("CursorDown", names);
            Assert.Contains("Confirm", names);
            Assert.Contains("Cancel", names);
        }
    }
}
