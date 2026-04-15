using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class BattleWaitStateTests
    {
        /// <summary>
        /// After Move+Act, the game skips the action menu and goes directly to the facing screen.
        /// BattleWait needs to detect this and skip menu navigation.
        /// </summary>
        [Theory]
        [InlineData("BattleMyTurn", false)]     // Normal: at action menu, needs menu nav
        [InlineData("BattleActing", false)]      // Acted but not moved: still has menu
        [InlineData("BattleAttacking", true)]    // After Move+Act: already on facing screen
        [InlineData("BattleMoving", true)]       // Could also be facing screen
        public void ShouldSkipMenuNavigation_DetectsCorrectly(string screenName, bool expectedSkip)
        {
            bool skip = BattleWaitLogic.ShouldSkipMenuNavigation(screenName);
            Assert.Equal(expectedSkip, skip);
        }

        /// <summary>
        /// BattleWait should accept these screen states as valid starting points.
        /// </summary>
        [Theory]
        [InlineData("BattleMyTurn", true)]
        [InlineData("BattleActing", true)]
        [InlineData("BattleAttacking", true)]   // After Move+Act facing screen
        [InlineData("BattleMoving", true)]      // Could be facing screen variant
        [InlineData("WorldMap", false)]
        [InlineData("TitleScreen", false)]
        [InlineData(null, false)]
        public void CanStartBattleWait_ValidatesScreen(string? screenName, bool expected)
        {
            bool valid = BattleWaitLogic.CanStartBattleWait(screenName);
            Assert.Equal(expected, valid);
        }

        [Fact]
        public void NeedsConfirmation_NoMoveNoAct_ReturnsTrue()
        {
            Assert.True(BattleWaitLogic.NeedsConfirmation(acted: false, moved: false, confirmed: false));
        }

        [Fact]
        public void NeedsConfirmation_Moved_ReturnsFalse()
        {
            Assert.False(BattleWaitLogic.NeedsConfirmation(acted: false, moved: true, confirmed: false));
        }

        [Fact]
        public void NeedsConfirmation_Acted_ReturnsFalse()
        {
            Assert.False(BattleWaitLogic.NeedsConfirmation(acted: true, moved: false, confirmed: false));
        }

        [Fact]
        public void NeedsConfirmation_BothMovedAndActed_ReturnsFalse()
        {
            Assert.False(BattleWaitLogic.NeedsConfirmation(acted: true, moved: true, confirmed: false));
        }

        [Fact]
        public void NeedsConfirmation_NoMoveNoAct_ButConfirmed_ReturnsFalse()
        {
            // Second battle_wait call should go through
            Assert.False(BattleWaitLogic.NeedsConfirmation(acted: false, moved: false, confirmed: true));
        }
    }
}
