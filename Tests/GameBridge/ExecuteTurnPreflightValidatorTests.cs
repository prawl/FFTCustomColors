using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pin the pre-flight checks for `execute_turn`: reject bundles that
    /// would attempt a move after a move is consumed, or an action after
    /// an action is consumed. Mirrors the battle_attack/battle_ability
    /// entry-reset check (commit 8cf9197) so execute_turn returns the
    /// canonical "Act already used" / "Move already used" message
    /// instead of falling through to a misleading "Not in Move mode"
    /// error deep in the sub-step.
    /// </summary>
    public class ExecuteTurnPreflightValidatorTests
    {
        private static TurnPlan MoveOnly(int x, int y)
            => new() { MoveX = x, MoveY = y };
        private static TurnPlan AbilityOnly(string name, int? tx = null, int? ty = null)
            => new() { AbilityName = name, TargetX = tx, TargetY = ty };
        private static TurnPlan MoveAndAbility(int mx, int my, string name, int tx, int ty)
            => new() { MoveX = mx, MoveY = my, AbilityName = name, TargetX = tx, TargetY = ty };

        [Fact]
        public void FreshTurn_MoveAndAbility_NoError()
        {
            Assert.Null(ExecuteTurnPreflightValidator.Validate(
                MoveAndAbility(5, 5, "Attack", 6, 5),
                hasMoved: false, hasActed: false));
        }

        [Fact]
        public void WaitOnly_FreshTurn_NoError()
        {
            Assert.Null(ExecuteTurnPreflightValidator.Validate(
                new TurnPlan(),
                hasMoved: false, hasActed: false));
        }

        [Fact]
        public void WaitOnly_AfterMoveAndAct_NoError()
        {
            // Wait-only bundle is always valid — turn ending is the legit path.
            Assert.Null(ExecuteTurnPreflightValidator.Validate(
                new TurnPlan(),
                hasMoved: true, hasActed: true));
        }

        [Fact]
        public void MoveRequested_AfterMoveConsumed_ReturnsMoveAlreadyUsed()
        {
            var error = ExecuteTurnPreflightValidator.Validate(
                MoveOnly(5, 5),
                hasMoved: true, hasActed: false);
            Assert.NotNull(error);
            Assert.Contains("Move already used", error);
        }

        [Fact]
        public void AbilityRequested_AfterActConsumed_ReturnsActAlreadyUsed()
        {
            var error = ExecuteTurnPreflightValidator.Validate(
                AbilityOnly("Attack", 6, 5),
                hasMoved: false, hasActed: true);
            Assert.NotNull(error);
            Assert.Contains("Act already used", error);
        }

        [Fact]
        public void MoveAndAbility_AfterMoveConsumed_ReportsMoveFirst()
        {
            // When both sub-steps would fail, surface the move error first
            // (matches step ordering in TurnPlan.ToSteps).
            var error = ExecuteTurnPreflightValidator.Validate(
                MoveAndAbility(5, 5, "Attack", 6, 5),
                hasMoved: true, hasActed: false);
            Assert.NotNull(error);
            Assert.Contains("Move already used", error);
        }

        [Fact]
        public void AbilityOnly_AfterMoveButFreshAct_NoError()
        {
            // Classic mid-turn ability after move is the canonical flow.
            Assert.Null(ExecuteTurnPreflightValidator.Validate(
                AbilityOnly("Attack", 6, 5),
                hasMoved: true, hasActed: false));
        }

        [Fact]
        public void MoveOnly_AfterActButFreshMove_NoError()
        {
            // Move-after-act is legal (acted first, then moves).
            Assert.Null(ExecuteTurnPreflightValidator.Validate(
                MoveOnly(5, 5),
                hasMoved: false, hasActed: true));
        }

        [Fact]
        public void SelfTargetAbility_AfterActConsumed_ReturnsActAlreadyUsed()
        {
            // Self-target abilities (Shout, no target coords) still count
            // as an action.
            var error = ExecuteTurnPreflightValidator.Validate(
                AbilityOnly("Shout"),
                hasMoved: false, hasActed: true);
            Assert.NotNull(error);
            Assert.Contains("Act already used", error);
        }

        [Fact]
        public void CanonicalMessages_MatchBattleAttackBattleAbility()
        {
            // Exact string match so downstream callers (shell response
            // parsers, tests) see the same canonical message shape as the
            // battle_attack / battle_ability pre-flight in commit 8cf9197.
            var moveErr = ExecuteTurnPreflightValidator.Validate(
                MoveOnly(5, 5),
                hasMoved: true, hasActed: false);
            var actErr = ExecuteTurnPreflightValidator.Validate(
                AbilityOnly("Attack", 6, 5),
                hasMoved: false, hasActed: true);
            Assert.Equal("Move already used this turn — only Act or Wait remain.", moveErr);
            Assert.Equal("Act already used this turn — only Move or Wait remain.", actErr);
        }
    }
}
