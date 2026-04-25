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
    public void BattleRewardObtainedBanner_DetectedWhenEncAIsOneAndMoveMode0()
    {
        // Chest-obtained banner: paused=0, moveMode=0, encA=1.
        // Verified at Zeklaus 2026-04-19 after two successive chest pickups
        // (longsword, leather clothing) — both showed encA=1.
        var result = ScreenDetectionLogic.Detect(
            party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
            battleMode: 1, moveMode: 0, paused: 0, gameOverFlag: 0,
            battleTeam: 0, battleActed: 0, battleMoved: 0,
            encA: 1, encB: 1, isPartySubScreen: false,
            submenuFlag: 1, menuCursor: 0);
        Assert.Equal("BattleRewardObtainedBanner", result);
    }

    [Fact]
    public void BattleRewardObtainedBanner_DetectedWhenChestOpenedAtTurnEnd()
    {
        // Live-captured 2026-04-25 Siedge Weald: Ramza moved onto a treasure
        // tile, confirmed "open chest" via Enter, banner showed "Obtained a
        // thief's cap!". Detection mis-classified as BattleEnemiesTurn because
        // the chest move auto-ended Ramza's turn — by the time the banner
        // rendered, the active unit had cycled to the next on the timeline
        // (an enemy here), so battleTeam=1 and gameOverFlag=1 (transient,
        // unclear why). The original Zeklaus 2026-04-19 capture had the
        // banner during Ramza's still-active turn (battleTeam=0,
        // gameOverFlag=0), which the existing rule covers.
        // Both cases share the chest-banner fingerprint:
        //   battleMode=1, moveMode=0, submenuFlag=1, menuCursor=0,
        //   encA=1, encB=1, paused=0
        // The new rule fires on this fingerprint regardless of team/gameOver,
        // because the banner is a global modal that overlays whatever turn
        // owner is active.
        var result = ScreenDetectionLogic.Detect(
            party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
            battleMode: 1, moveMode: 0, paused: 0, gameOverFlag: 1,
            battleTeam: 1, battleActed: 0, battleMoved: 0,
            encA: 1, encB: 1, isPartySubScreen: false,
            submenuFlag: 1, menuCursor: 0);
        Assert.Equal("BattleRewardObtainedBanner", result);
    }

    [Fact]
    public void BattleCrystalMoveConfirm_DetectedWhenTeamIsNotZero()
    {
        // Live-captured 2026-04-25 Siedge Weald: Ramza moved onto a
        // crystallized unit's tile, "Use the crystal to fully restore
        // HP and MP?" Yes/No dialog appeared, detection returned
        // BattleMoving. Same root cause as the chest-banner case: the
        // team-guarded crystal-pickup block required battleTeam==0,
        // but the move auto-ended Ramza's turn so battleTeam had cycled
        // (here =3, an unusual neutral/staging value).
        var result = ScreenDetectionLogic.Detect(
            party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
            battleMode: 1, moveMode: 0, paused: 0, gameOverFlag: 1,
            battleTeam: 3, battleActed: 1, battleMoved: 1,
            encA: 3, encB: 3, isPartySubScreen: false,
            submenuFlag: 1, menuCursor: 0);
        Assert.Equal("BattleCrystalMoveConfirm", result);
    }

    [Fact]
    public void BattleCrystalMoveConfirm_DetectedAfterSaveRestoreClearsSubmenuFlag()
    {
        // Live-captured 2026-04-25 Siedge Weald: same crystal dialog as
        // above, but captured after save+restore. submenuFlag cleared
        // from 1 to 0 on resume; gameOverFlag also cleared from 1 to 0.
        // Both transient flags drop on save-restore but the dialog
        // persists. Detection must survive that — the load-bearing
        // fingerprint is battleMode/moveMode/encA/paused/menuCursor.
        var result = ScreenDetectionLogic.Detect(
            party: 0, ui: 0, rawLocation: 255, slot0: 255, slot9: 0xFFFFFFFF,
            battleMode: 1, moveMode: 0, paused: 0, gameOverFlag: 0,
            battleTeam: 3, battleActed: 255, battleMoved: 255,
            encA: 3, encB: 3, isPartySubScreen: false,
            submenuFlag: 0, menuCursor: 0);
        Assert.Equal("BattleCrystalMoveConfirm", result);
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
