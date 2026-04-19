using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pins the "should raw Enter bump the dialogue-box counter?" logic
    /// per TODO §0 session 45 follow-up. The raw-key path in CommandWatcher
    /// (ExecuteKeyCommand) forwards arbitrary keypresses to the game; when
    /// the player hits Enter on a dialogue screen, the tracker must advance
    /// the box index just as `advance_dialogue` does.
    ///
    /// Guard against double-bumping: the advance_dialogue command handler
    /// already bumps BEFORE dispatching the Enter. The raw-key hook must
    /// only fire when the key WASN'T already counted by that path.
    /// </summary>
    public class DialogueTrackerKeyHookTests
    {
        private const int VK_ENTER = 0x0D;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_UP = 0x26;

        [Fact]
        public void EnterOnCutscene_BumpsTracker()
        {
            Assert.True(DialogueTrackerKeyHook.ShouldAdvance(VK_ENTER, "Cutscene"));
        }

        [Fact]
        public void EnterOnBattleDialogue_BumpsTracker()
        {
            Assert.True(DialogueTrackerKeyHook.ShouldAdvance(VK_ENTER, "BattleDialogue"));
        }

        [Fact]
        public void EnterOnBattleChoice_BumpsTracker()
        {
            Assert.True(DialogueTrackerKeyHook.ShouldAdvance(VK_ENTER, "BattleChoice"));
        }

        [Theory]
        [InlineData("WorldMap")]
        [InlineData("BattleMyTurn")]
        [InlineData("PartyMenuUnits")]
        [InlineData("BattlePaused")]
        [InlineData("CharacterStatus")]
        public void EnterOffDialogueScreen_DoesNotBump(string screenName)
        {
            Assert.False(DialogueTrackerKeyHook.ShouldAdvance(VK_ENTER, screenName));
        }

        [Theory]
        [InlineData(VK_ESCAPE)]
        [InlineData(VK_UP)]
        [InlineData(0x20)] // Space
        public void NonEnterKeys_DoNotBump_EvenOnDialogueScreen(int vk)
        {
            Assert.False(DialogueTrackerKeyHook.ShouldAdvance(vk, "Cutscene"));
            Assert.False(DialogueTrackerKeyHook.ShouldAdvance(vk, "BattleDialogue"));
        }

        [Fact]
        public void NullOrEmptyScreen_DoesNotBump()
        {
            Assert.False(DialogueTrackerKeyHook.ShouldAdvance(VK_ENTER, null));
            Assert.False(DialogueTrackerKeyHook.ShouldAdvance(VK_ENTER, ""));
        }

        [Fact]
        public void EndToEnd_BumpsTrackerOnEnterInDialogueScreen()
        {
            // Exercise the helper in a realistic flow: simulate three Enter
            // presses during a Cutscene, confirm box index advances.
            var tracker = new DialogueProgressTracker();
            const int eventId = 42;
            Assert.Equal(0, tracker.GetBoxIndex(eventId));

            DialogueTrackerKeyHook.HandleKeyPress(tracker, VK_ENTER, "Cutscene", eventId);
            Assert.Equal(1, tracker.GetBoxIndex(eventId));

            DialogueTrackerKeyHook.HandleKeyPress(tracker, VK_ENTER, "Cutscene", eventId);
            DialogueTrackerKeyHook.HandleKeyPress(tracker, VK_ENTER, "Cutscene", eventId);
            Assert.Equal(3, tracker.GetBoxIndex(eventId));
        }

        [Fact]
        public void EndToEnd_IgnoresNonEnter()
        {
            var tracker = new DialogueProgressTracker();
            const int eventId = 42;

            DialogueTrackerKeyHook.HandleKeyPress(tracker, VK_ESCAPE, "Cutscene", eventId);
            DialogueTrackerKeyHook.HandleKeyPress(tracker, VK_UP, "BattleDialogue", eventId);
            Assert.Equal(0, tracker.GetBoxIndex(eventId));
        }

        [Fact]
        public void EndToEnd_IgnoresEnterOffDialogueScreen()
        {
            var tracker = new DialogueProgressTracker();
            const int eventId = 42;

            DialogueTrackerKeyHook.HandleKeyPress(tracker, VK_ENTER, "WorldMap", eventId);
            DialogueTrackerKeyHook.HandleKeyPress(tracker, VK_ENTER, "BattleMyTurn", eventId);
            Assert.Equal(0, tracker.GetBoxIndex(eventId));
        }

        [Fact]
        public void EndToEnd_OutOfRangeEventId_DoesNotBump()
        {
            // eventId < 1 or >= 400 is not a real scene — the existing
            // advance_dialogue guard filters these to prevent spurious
            // bumps from uninitialized memory. The hook must match.
            var tracker = new DialogueProgressTracker();
            DialogueTrackerKeyHook.HandleKeyPress(tracker, VK_ENTER, "Cutscene", 0);
            DialogueTrackerKeyHook.HandleKeyPress(tracker, VK_ENTER, "Cutscene", 400);
            DialogueTrackerKeyHook.HandleKeyPress(tracker, VK_ENTER, "Cutscene", 9999);
            // Each call sets _eventId to its argument then tracks from there;
            // ensure they all return 0 for their respective eventIds.
            Assert.Equal(0, tracker.GetBoxIndex(0));
            Assert.Equal(0, tracker.GetBoxIndex(400));
            Assert.Equal(0, tracker.GetBoxIndex(9999));
        }
    }
}
