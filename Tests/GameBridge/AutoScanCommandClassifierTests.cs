using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// Pure decision table: which nav-action commands should trigger a
// post-completion auto-scan? The pragmatic rule is "yes by default,
// except for transition-commit actions where the battle array may
// not yet reflect the new state".
//
// S57 surfaced auto_place_units returning `[auto-scan] No ally
// found in scan` because ExecuteNavActionWithAutoScan ran scan_move
// immediately after the battle-commence poll exited on BattleMyTurn —
// but the static battle array is still populating for several
// hundred ms during the formation-to-battle transition.
//
// Fix: auto_place_units skips the auto-scan. The user's next `screen`
// / `scan_move` call will pick up valid data after the array settles.
public class AutoScanCommandClassifierTests
{
    [Theory]
    [InlineData("battle_wait")]
    [InlineData("open_eqa")]
    [InlineData("open_job_selection")]
    [InlineData("open_character_status")]
    [InlineData("swap_unit_to")]
    [InlineData("advance_dialogue")]
    [InlineData("execute_action")]
    public void ShouldAutoScanAfter_ReturnsTrue_ForNormalNavActions(string action)
    {
        Assert.True(AutoScanCommandClassifier.ShouldAutoScanAfter(action));
    }

    [Fact]
    public void ShouldAutoScanAfter_AutoPlaceUnits_ReturnsFalse()
    {
        // Post-commit transition: battle array still populating.
        // Auto-scan here reliably produces "No ally found in scan".
        Assert.False(AutoScanCommandClassifier.ShouldAutoScanAfter("auto_place_units"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ShouldAutoScanAfter_NullOrEmpty_ReturnsFalse(string? action)
    {
        Assert.False(AutoScanCommandClassifier.ShouldAutoScanAfter(action));
    }

    [Fact]
    public void ShouldAutoScanAfter_UnknownAction_ReturnsTrue()
    {
        // Default-permissive: unknown actions auto-scan unless
        // explicitly blocked. Keeps future-added nav actions working
        // without a classifier update.
        Assert.True(AutoScanCommandClassifier.ShouldAutoScanAfter("some_future_nav_action"));
    }
}
