using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for <see cref="BattleWaitLogic"/> — a small pure helper that
    /// classifies when battle_wait can start and whether to skip the action
    /// menu. No direct tests existed prior to session 47.
    /// </summary>
    public class BattleWaitLogicTests
    {
        [Theory]
        [InlineData("BattleAttacking", true)]
        [InlineData("BattleMoving", true)]
        [InlineData("BattleMyTurn", false)]
        [InlineData("BattleActing", false)]
        [InlineData("WorldMap", false)]
        [InlineData(null, false)]
        [InlineData("", false)]
        public void ShouldSkipMenuNavigation_ChecksAutoFacingScreens(string? screen, bool expected)
        {
            Assert.Equal(expected, BattleWaitLogic.ShouldSkipMenuNavigation(screen));
        }

        [Fact]
        public void NeedsConfirmation_UnitDidNothing_Confirms()
        {
            // No move, no action — suspicious. Prompt before ending turn.
            Assert.True(BattleWaitLogic.NeedsConfirmation(acted: false, moved: false, confirmed: false));
        }

        [Fact]
        public void NeedsConfirmation_UnitMoved_DoesNotConfirm()
        {
            Assert.False(BattleWaitLogic.NeedsConfirmation(acted: false, moved: true, confirmed: false));
        }

        [Fact]
        public void NeedsConfirmation_UnitActed_DoesNotConfirm()
        {
            Assert.False(BattleWaitLogic.NeedsConfirmation(acted: true, moved: false, confirmed: false));
        }

        [Fact]
        public void NeedsConfirmation_UnitActedAndMoved_DoesNotConfirm()
        {
            Assert.False(BattleWaitLogic.NeedsConfirmation(acted: true, moved: true, confirmed: false));
        }

        [Fact]
        public void NeedsConfirmation_AlreadyConfirmed_BypassesCheck()
        {
            // Second call with confirmed=true bypasses the safety even if the
            // unit did nothing — the user explicitly said "yes I want to skip."
            Assert.False(BattleWaitLogic.NeedsConfirmation(acted: false, moved: false, confirmed: true));
        }

        [Theory]
        [InlineData("BattleMyTurn", true)]
        [InlineData("BattleActing", true)]
        [InlineData("BattleAttacking", true)]
        [InlineData("BattleMoving", true)]
        [InlineData("BattleAbilities", false)]
        [InlineData("BattleCasting", false)]
        [InlineData("BattleEnemiesTurn", false)]
        [InlineData("WorldMap", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void CanStartBattleWait_AllowsOnlyTurnAndFacingStates(string? screen, bool expected)
        {
            Assert.Equal(expected, BattleWaitLogic.CanStartBattleWait(screen));
        }
    }
}
