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
        [InlineData("Battle_MyTurn", false)]     // Normal: at action menu, needs menu nav
        [InlineData("Battle_Acting", false)]      // Acted but not moved: still has menu
        [InlineData("Battle_Targeting", true)]    // After Move+Act: already on facing screen
        [InlineData("Battle_Moving", true)]       // Could also be facing screen
        public void ShouldSkipMenuNavigation_DetectsCorrectly(string screenName, bool expectedSkip)
        {
            bool skip = BattleWaitLogic.ShouldSkipMenuNavigation(screenName);
            Assert.Equal(expectedSkip, skip);
        }

        /// <summary>
        /// BattleWait should accept these screen states as valid starting points.
        /// </summary>
        [Theory]
        [InlineData("Battle_MyTurn", true)]
        [InlineData("Battle_Acting", true)]
        [InlineData("Battle_Targeting", true)]   // After Move+Act facing screen
        [InlineData("Battle_Moving", true)]      // Could be facing screen variant
        [InlineData("WorldMap", false)]
        [InlineData("TitleScreen", false)]
        [InlineData(null, false)]
        public void CanStartBattleWait_ValidatesScreen(string? screenName, bool expected)
        {
            bool valid = BattleWaitLogic.CanStartBattleWait(screenName);
            Assert.Equal(expected, valid);
        }
    }
}
