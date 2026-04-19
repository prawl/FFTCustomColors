using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// Crystal-pickup sequence: 4 modal states triggered when a unit moves onto
// a crystallized unit's tile. All share battleMode=1, submenuFlag=1,
// menuCursor=0, battleTeam=0 with normal BattleMoving but differ in
// paused, moveMode, and encA (widget-stack-depth byte). Fingerprints
// live-captured 2026-04-19 at Zeklaus event 40. Table in
// memory/project_crystal_states_undetected.md.
public class CrystalStateDetectionTests
{
    // Normal BattleMoving (player picking a tile) uses moveMode=255.
    // All crystal states use moveMode=0. This is the primary discriminator.
    [Fact]
    public void BattleMoving_NormalPickerWithMoveMode255_StillDetected()
    {
        var result = ScreenDetectionLogic.Detect(
            party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
            battleMode: 1, moveMode: 255, paused: 0, gameOverFlag: 0,
            battleTeam: 0, battleActed: 0, battleMoved: 0,
            encA: 2, encB: 2, isPartySubScreen: false,
            submenuFlag: 1, menuCursor: 0);
        Assert.Equal("BattleMoving", result);
    }

    [Fact]
    public void BattleCrystalMoveConfirm_DetectedWhenMoveMode0AndEncANonZero()
    {
        // S1 fingerprint: battleMode=1 submenuFlag=1 menuCursor=0 battleTeam=0
        //                 paused=0 moveMode=0 encA=2
        var result = ScreenDetectionLogic.Detect(
            party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
            battleMode: 1, moveMode: 0, paused: 0, gameOverFlag: 0,
            battleTeam: 0, battleActed: 0, battleMoved: 0,
            encA: 2, encB: 2, isPartySubScreen: false,
            submenuFlag: 1, menuCursor: 0);
        Assert.Equal("BattleCrystalMoveConfirm", result);
    }

    [Fact]
    public void BattleCrystalReward_DetectedWhenPausedAndEncABelow5()
    {
        // S2 fingerprint: paused=1 moveMode=0 encA=4
        var result = ScreenDetectionLogic.Detect(
            party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
            battleMode: 1, moveMode: 0, paused: 1, gameOverFlag: 0,
            battleTeam: 0, battleActed: 0, battleMoved: 0,
            encA: 4, encB: 4, isPartySubScreen: false,
            submenuFlag: 1, menuCursor: 0);
        Assert.Equal("BattleCrystalReward", result);
    }

    [Fact]
    public void BattleAbilityAcquireConfirm_DetectedWhenPausedAndEncAAtLeast5()
    {
        // S3 fingerprint: paused=1 moveMode=0 encA=7
        var result = ScreenDetectionLogic.Detect(
            party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
            battleMode: 1, moveMode: 0, paused: 1, gameOverFlag: 0,
            battleTeam: 0, battleActed: 0, battleMoved: 0,
            encA: 7, encB: 4, isPartySubScreen: false,
            submenuFlag: 1, menuCursor: 0);
        Assert.Equal("BattleAbilityAcquireConfirm", result);
    }

    [Fact]
    public void BattleAbilityLearnedBanner_DetectedWhenEncAIsZeroAndMoveMode0()
    {
        // S4 fingerprint: paused=0 moveMode=0 encA=0
        var result = ScreenDetectionLogic.Detect(
            party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
            battleMode: 1, moveMode: 0, paused: 0, gameOverFlag: 0,
            battleTeam: 0, battleActed: 0, battleMoved: 0,
            encA: 0, encB: 0, isPartySubScreen: false,
            submenuFlag: 1, menuCursor: 0);
        Assert.Equal("BattleAbilityLearnedBanner", result);
    }

    // Regression guards: the existing BattlePaused and BattleMoving rules
    // must still fire in their normal contexts.
    [Fact]
    public void BattlePaused_StillFires_WhenNotBattleMode1()
    {
        // Classic pause menu: paused=1 but battleMode=0.
        var result = ScreenDetectionLogic.Detect(
            party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
            battleMode: 0, moveMode: 0, paused: 1, gameOverFlag: 0,
            battleTeam: 0, battleActed: 0, battleMoved: 0,
            encA: 3, encB: 3, isPartySubScreen: false,
            submenuFlag: 0, menuCursor: 0);
        Assert.Equal("BattlePaused", result);
    }
}
