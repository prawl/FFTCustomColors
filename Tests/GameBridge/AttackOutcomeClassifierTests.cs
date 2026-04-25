using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pin the post-attack hit/miss/KO classification. The previous logic
    /// keyed off a HP delta from ReadLiveHp; when the heap-search fingerprint
    /// failed to find a level-matched copy of the target, postHp fell back to
    /// preHp and the bridge mis-reported genuine hits as MISSED. The screen
    /// state after the attack animation is the authoritative signal: HIT
    /// advances to BattleMoving (facing confirm), MISS re-opens BattleAttacking
    /// for re-targeting, and BattleVictory means the target was KO'd as the
    /// last enemy.
    /// </summary>
    public class AttackOutcomeClassifierTests
    {
        [Fact]
        public void BattleAttackingPostAnimation_IsMiss()
        {
            var outcome = AttackOutcomeClassifier.Classify(
                postScreenName: "BattleAttacking",
                preHp: 512,
                postHp: 512);
            Assert.Equal(AttackOutcome.Miss, outcome);
        }

        [Fact]
        public void BattleMovingPostAnimation_WithHpDelta_IsHit()
        {
            var outcome = AttackOutcomeClassifier.Classify(
                postScreenName: "BattleMoving",
                preHp: 512,
                postHp: 480);
            Assert.Equal(AttackOutcome.Hit, outcome);
        }

        [Fact]
        public void BattleMovingPostAnimation_WithoutHpDelta_StillHit()
        {
            // The fix: even when ReadLiveHp falls back to preHp because the
            // heap-search couldn't fingerprint the target's live struct, the
            // BattleMoving screen says the game advanced to facing-confirm,
            // which only happens after a hit. Don't claim Miss without
            // affirmative miss evidence.
            var outcome = AttackOutcomeClassifier.Classify(
                postScreenName: "BattleMoving",
                preHp: 512,
                postHp: 512);
            Assert.Equal(AttackOutcome.Hit, outcome);
        }

        [Fact]
        public void BattleMovingPostAnimation_WithHpZero_IsKo()
        {
            var outcome = AttackOutcomeClassifier.Classify(
                postScreenName: "BattleMoving",
                preHp: 60,
                postHp: 0);
            Assert.Equal(AttackOutcome.Ko, outcome);
        }

        [Fact]
        public void BattleActingPostAnimation_BehavesLikeBattleMoving()
        {
            // BattleActing is the alternate landing screen when the active
            // unit has already moved (action consumed but turn not ended).
            var outcomeHit = AttackOutcomeClassifier.Classify(
                postScreenName: "BattleActing",
                preHp: 512,
                postHp: 512);
            Assert.Equal(AttackOutcome.Hit, outcomeHit);

            var outcomeKo = AttackOutcomeClassifier.Classify(
                postScreenName: "BattleActing",
                preHp: 60,
                postHp: 0);
            Assert.Equal(AttackOutcome.Ko, outcomeKo);
        }

        [Fact]
        public void BattleVictoryPostAnimation_WithHpZero_IsKo()
        {
            var outcome = AttackOutcomeClassifier.Classify(
                postScreenName: "BattleVictory",
                preHp: 60,
                postHp: 0);
            Assert.Equal(AttackOutcome.Ko, outcome);
        }

        [Fact]
        public void BattleVictoryPostAnimation_TargetStillAlive_IsHit()
        {
            // Live-observed at Siedge Weald 2026-04-25: the screen detector
            // flicker-touched BattleVictory mid-attack and went straight back
            // to BattleMyTurn (target was alive at full HP). Trusting the
            // BattleVictory signal alone produced false KO reports. Corroborate
            // with HP evidence — postHp > 0 means target survived, so the
            // BattleVictory was a transient false-positive.
            var outcome = AttackOutcomeClassifier.Classify(
                postScreenName: "BattleVictory",
                preHp: 512,
                postHp: 512);
            Assert.Equal(AttackOutcome.Hit, outcome);
        }

        [Fact]
        public void BattleVictoryPostAnimation_HpReadFailed_IsHit()
        {
            // ReadLiveHp returned -1 — without affirmative HP=0 evidence we
            // can't tell a real victory from a flicker. Degrade to Hit; the
            // response.Screen field will surface BattleVictory if it persists
            // and the user will see the banner regardless.
            var outcome = AttackOutcomeClassifier.Classify(
                postScreenName: "BattleVictory",
                preHp: 60,
                postHp: -1);
            Assert.Equal(AttackOutcome.Hit, outcome);
        }

        [Fact]
        public void GameOverPostAnimation_IsUnknown()
        {
            // GameOver mid-attack is rare (counter killed your last unit
            // mid-animation, etc). The attack itself may have hit or missed
            // before the reaction landed. Conservative: don't guess.
            var outcome = AttackOutcomeClassifier.Classify(
                postScreenName: "GameOver",
                preHp: 512,
                postHp: 512);
            Assert.Equal(AttackOutcome.Unknown, outcome);
        }

        [Fact]
        public void NullScreenName_HpDeltaFallback_NonZeroDeltaIsHit()
        {
            // When the post-animation screen detection failed to surface a
            // recognized state (null), use HP-diff as a backup signal — but
            // only the affirmative direction (delta != 0). Equal HP without
            // a screen signal is Unknown, NOT Miss.
            var outcome = AttackOutcomeClassifier.Classify(
                postScreenName: null,
                preHp: 512,
                postHp: 480);
            Assert.Equal(AttackOutcome.Hit, outcome);
        }

        [Fact]
        public void NullScreenName_HpDeltaFallback_ZeroPostHpIsKo()
        {
            var outcome = AttackOutcomeClassifier.Classify(
                postScreenName: null,
                preHp: 60,
                postHp: 0);
            Assert.Equal(AttackOutcome.Ko, outcome);
        }

        [Fact]
        public void NullScreenName_NoHpDelta_IsUnknown()
        {
            // The bug fix: previously postHp == preHp returned Miss, which
            // mis-classified hits whose live HP couldn't be located. Without
            // affirmative miss evidence, we now return Unknown.
            var outcome = AttackOutcomeClassifier.Classify(
                postScreenName: null,
                preHp: 512,
                postHp: 512);
            Assert.Equal(AttackOutcome.Unknown, outcome);
        }

        [Fact]
        public void NullScreenName_HpReadFailed_IsUnknown()
        {
            var outcome = AttackOutcomeClassifier.Classify(
                postScreenName: null,
                preHp: 512,
                postHp: -1);
            Assert.Equal(AttackOutcome.Unknown, outcome);
        }

        [Fact]
        public void UnrecognizedScreenName_FallsBackToHpDelta()
        {
            var outcome = AttackOutcomeClassifier.Classify(
                postScreenName: "BattleDialogue",
                preHp: 512,
                postHp: 480);
            Assert.Equal(AttackOutcome.Hit, outcome);
        }
    }
}
