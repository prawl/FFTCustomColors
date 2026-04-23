using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// TODO §0 session 31: during battle_wait animations, unit-slot/ui
// bytes transiently match the CharacterStatus fingerprint, so detection
// flickers sourceScreen=CharacterStatus → targetScreen=BattleMyTurn
// with 15-23s latencies in session logs. When the previous settled
// screen was in a battle state AND no key has been pressed since,
// a CharacterStatus detection is treated as a leak and held at the
// previous battle state.
public class CharacterStatusLeakGuardTests
{
    [Theory]
    [InlineData("BattleMyTurn")]
    [InlineData("BattleMoving")]
    [InlineData("BattleAttacking")]
    [InlineData("BattleCasting")]
    [InlineData("BattleAbilities")]
    [InlineData("BattleActing")]
    [InlineData("BattleWaiting")]
    [InlineData("BattleEnemiesTurn")]
    [InlineData("BattleAlliesTurn")]
    [InlineData("BattlePaused")]
    [InlineData("BattleDialogue")]
    public void BattlePrev_NoKeys_CharacterStatusLeak_HoldsBattleState(string prevBattle)
    {
        var result = CharacterStatusLeakGuard.Filter(
            previousDetected: prevBattle,
            currentDetected: "CharacterStatus",
            keysSincePreviousDetected: 0);
        Assert.Equal(prevBattle, result);
    }

    [Fact]
    public void BattlePrev_NoKeys_CombatSetsLeak_HoldsBattleState()
    {
        var result = CharacterStatusLeakGuard.Filter(
            previousDetected: "BattleMyTurn",
            currentDetected: "CombatSets",
            keysSincePreviousDetected: 0);
        Assert.Equal("BattleMyTurn", result);
    }

    [Fact]
    public void BattlePrev_WithKeyPress_AllowsDrillIn()
    {
        // Real transition: BattleMyTurn → Status action → CharacterStatus.
        // User pressed Enter, so the guard must NOT fire.
        var result = CharacterStatusLeakGuard.Filter(
            previousDetected: "BattleMyTurn",
            currentDetected: "CharacterStatus",
            keysSincePreviousDetected: 1);
        Assert.Equal("CharacterStatus", result);
    }

    [Fact]
    public void NonBattlePrev_AllowsCharacterStatus()
    {
        // Normal party menu drill-in: PartyMenuUnits → SelectUnit → CharacterStatus.
        var result = CharacterStatusLeakGuard.Filter(
            previousDetected: "PartyMenuUnits",
            currentDetected: "CharacterStatus",
            keysSincePreviousDetected: 1);
        Assert.Equal("CharacterStatus", result);
    }

    [Fact]
    public void NullPrev_AllowsCharacterStatus()
    {
        var result = CharacterStatusLeakGuard.Filter(
            previousDetected: null,
            currentDetected: "CharacterStatus",
            keysSincePreviousDetected: 0);
        Assert.Equal("CharacterStatus", result);
    }

    [Fact]
    public void NonLeakTransition_PassesThroughUnchanged()
    {
        // Guard only affects CharacterStatus/CombatSets detections.
        var result = CharacterStatusLeakGuard.Filter(
            previousDetected: "BattleMyTurn",
            currentDetected: "BattleMoving",
            keysSincePreviousDetected: 0);
        Assert.Equal("BattleMoving", result);
    }

    [Fact]
    public void BattleToWorldMap_PassesThrough()
    {
        var result = CharacterStatusLeakGuard.Filter(
            previousDetected: "BattleMyTurn",
            currentDetected: "WorldMap",
            keysSincePreviousDetected: 0);
        Assert.Equal("WorldMap", result);
    }

    // S59: additional edge cases exercised by live wire-up in CommandWatcher.

    [Fact]
    public void EmptyStringPrev_AllowsCharacterStatus()
    {
        // Empty string (equivalent to no previous screen recorded) should
        // not trigger the filter — nothing to hold.
        var result = CharacterStatusLeakGuard.Filter(
            previousDetected: "",
            currentDetected: "CharacterStatus",
            keysSincePreviousDetected: 0);
        Assert.Equal("CharacterStatus", result);
    }

    [Fact]
    public void BattlePrev_HighKeyCount_AllowsCharacterStatus()
    {
        // Large key counts (post-scan_move with many C+Up cycles) still
        // count as "keys pressed" — guard does NOT fire.
        var result = CharacterStatusLeakGuard.Filter(
            previousDetected: "BattleMyTurn",
            currentDetected: "CharacterStatus",
            keysSincePreviousDetected: 100);
        Assert.Equal("CharacterStatus", result);
    }

    [Fact]
    public void NonBattleNonCharacterStatusPrev_AllowsAnyCurrent()
    {
        // Guard only matters when prev was a battle state — everything
        // else passes through regardless of keys-since.
        var result = CharacterStatusLeakGuard.Filter(
            previousDetected: "WorldMap",
            currentDetected: "CharacterStatus",
            keysSincePreviousDetected: 0);
        Assert.Equal("CharacterStatus", result);
    }
}
