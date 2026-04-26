using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pure helper for the multi-sample debounce: given up to three
    /// classifications of the same memory state, pick the majority.
    /// Used by <c>DetectScreenSettled</c> when a screen-query command's
    /// fresh read disagrees with the last-committed screen — a third
    /// read tiebreaks.
    /// </summary>
    public class MajorityVoteTests
    {
        [Fact]
        public void AllThreeAgree_ReturnsThatValue()
        {
            Assert.Equal("BattleMyTurn",
                MajorityVote.Pick("BattleMyTurn", "BattleMyTurn", "BattleMyTurn"));
        }

        [Fact]
        public void TwoOfThreeAgree_ReturnsMajority()
        {
            Assert.Equal("BattleMyTurn",
                MajorityVote.Pick("BattleMyTurn", "BattleMyTurn", "BattleAttacking"));
            Assert.Equal("BattleMyTurn",
                MajorityVote.Pick("BattleMyTurn", "BattleAttacking", "BattleMyTurn"));
            Assert.Equal("BattleMyTurn",
                MajorityVote.Pick("BattleAttacking", "BattleMyTurn", "BattleMyTurn"));
        }

        [Fact]
        public void AllThreeDifferent_ReturnsLatest()
        {
            // No majority. Prefer the most-recent sample (third) since it's
            // the freshest read — most likely to reflect the true current
            // state by the time we commit.
            Assert.Equal("BattleVictory",
                MajorityVote.Pick("BattleAttacking", "BattleMyTurn", "BattleVictory"));
        }

        [Fact]
        public void NullSecond_TwoUnanimous_ReturnsAgreement()
        {
            // Defensive: when the second read failed, fall back to first/third agreement.
            Assert.Equal("BattleMyTurn",
                MajorityVote.Pick("BattleMyTurn", null, "BattleMyTurn"));
        }

        [Fact]
        public void NullThird_FirstAndSecondAgree_ReturnsThat()
        {
            Assert.Equal("BattleMyTurn",
                MajorityVote.Pick("BattleMyTurn", "BattleMyTurn", null));
        }

        [Fact]
        public void NullThird_FirstAndSecondDisagree_ReturnsSecond()
        {
            // Two different non-null reads, third missing — prefer the
            // newer read (second).
            Assert.Equal("BattleAttacking",
                MajorityVote.Pick("BattleMyTurn", "BattleAttacking", null));
        }

        [Fact]
        public void OnlyFirst_ReturnsFirst()
        {
            Assert.Equal("BattleMyTurn",
                MajorityVote.Pick("BattleMyTurn", null, null));
        }

        [Fact]
        public void AllNull_ReturnsNull()
        {
            Assert.Null(MajorityVote.Pick(null, null, null));
        }

        [Fact]
        public void NullFirst_RestAgree_ReturnsAgreement()
        {
            Assert.Equal("BattleMyTurn",
                MajorityVote.Pick(null, "BattleMyTurn", "BattleMyTurn"));
        }
    }
}
