using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// Tracks which in-game dialogue box the player is currently viewing.
// The tracker has no access to game memory — it's a pure counter that
// the bridge increments on advance-like actions and resets when the
// eventId changes (scene transition). Lets `screen` surface only the
// current box so the user can pace the game without spoilers.
public class DialogueProgressTrackerTests
{
    [Fact]
    public void NewTracker_ReturnsZero_ForFirstObservedEvent()
    {
        var t = new DialogueProgressTracker();
        Assert.Equal(0, t.GetBoxIndex(eventId: 10));
    }

    [Fact]
    public void Advance_IncrementsBoxIndex()
    {
        var t = new DialogueProgressTracker();
        t.GetBoxIndex(10);  // establish event
        t.Advance(10);
        Assert.Equal(1, t.GetBoxIndex(10));
        t.Advance(10);
        Assert.Equal(2, t.GetBoxIndex(10));
    }

    [Fact]
    public void GetBoxIndex_ResetsToZero_OnEventIdChange()
    {
        var t = new DialogueProgressTracker();
        t.GetBoxIndex(10);
        t.Advance(10);
        t.Advance(10);
        Assert.Equal(0, t.GetBoxIndex(11)); // new event, counter reset
    }

    [Fact]
    public void Advance_OnDifferentEvent_StartsThatEventAtOne()
    {
        var t = new DialogueProgressTracker();
        t.GetBoxIndex(10);
        t.Advance(10);   // event 10 at box 1
        t.GetBoxIndex(11); // switch to event 11 (resets to 0)
        t.Advance(11);
        Assert.Equal(1, t.GetBoxIndex(11));
    }

    [Fact]
    public void GetBoxIndex_PreservesCounterForSameEvent()
    {
        var t = new DialogueProgressTracker();
        t.GetBoxIndex(10);
        t.Advance(10);
        t.Advance(10);
        // Called again for same event — don't reset.
        Assert.Equal(2, t.GetBoxIndex(10));
        Assert.Equal(2, t.GetBoxIndex(10));
    }

    [Fact]
    public void ReturningToPriorEvent_ResetsTheCounter()
    {
        // Scene 10 → 11 → back to 10. We can't tell whether "back to 10"
        // is a replay from the start or a continuation — simplest honest
        // choice is to reset. The alternative (stashing per-event counters)
        // would lie when the game replays an event fresh.
        var t = new DialogueProgressTracker();
        t.GetBoxIndex(10);
        t.Advance(10);
        t.Advance(10);
        t.GetBoxIndex(11); // leave 10
        t.GetBoxIndex(10); // return — should reset
        Assert.Equal(0, t.GetBoxIndex(10));
    }
}
