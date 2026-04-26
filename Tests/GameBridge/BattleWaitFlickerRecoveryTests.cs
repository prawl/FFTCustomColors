using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// `battle_wait` nav action errors out immediately when the screen at
    /// start is BattleVictory / GameOver / BattleDesertion. But those
    /// states flicker briefly (~1-3s) at end-of-turn before the screen
    /// settles back to BattleMyTurn (or genuinely terminal). Live-flagged
    /// 2026-04-25 playtest: agent saw 3 consecutive ranged attacks abort
    /// with "Cannot battle_wait from screen (current: BattleVictory)" —
    /// each one needed a manual `screen` + `battle_wait` recovery dance
    /// adding 8-15s of wall clock.
    ///
    /// IsRecoverableFlicker classifies states that warrant a retry-after-
    /// settle vs states where the wait should fail fast (e.g. mid-cutscene).
    /// </summary>
    public class BattleWaitFlickerRecoveryTests
    {
        [Theory]
        [InlineData("BattleVictory")]
        [InlineData("BattleDesertion")]
        [InlineData("GameOver")]
        public void TerminalFlickerStates_AreRecoverable(string screen)
        {
            // These can transiently appear mid-wait before settling. Worth
            // a settle + recheck — could resolve to BattleMyTurn.
            Assert.True(BattleWaitFlickerRecovery.IsRecoverableFlicker(screen));
        }

        [Theory]
        [InlineData("BattleMyTurn")]
        [InlineData("BattleActing")]
        [InlineData("BattleAttacking")]
        [InlineData("BattleMoving")]
        public void NormalBattleStates_AreNotFlicker(string screen)
        {
            // CanStartBattleWait already accepts these — never need to
            // hit the flicker-recovery path.
            Assert.False(BattleWaitFlickerRecovery.IsRecoverableFlicker(screen));
        }

        [Theory]
        [InlineData("WorldMap")]
        [InlineData("Cutscene")]
        [InlineData("PartyMenuUnits")]
        [InlineData("BattleAbilities")]
        [InlineData(null)]
        [InlineData("")]
        public void NonBattleOrSubmenuStates_AreNotFlicker(string? screen)
        {
            // We're genuinely out of battle / in a submenu — fail fast,
            // don't sleep on these.
            Assert.False(BattleWaitFlickerRecovery.IsRecoverableFlicker(screen));
        }
    }
}
