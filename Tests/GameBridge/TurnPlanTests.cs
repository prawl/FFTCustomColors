using FFTColorCustomizer.GameBridge;
using System.Linq;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for <see cref="TurnPlan.ToSteps"/> — pure planner that converts
    /// a bundled `execute_turn` intent into an ordered list of existing
    /// bridge sub-actions (battle_move, battle_ability, battle_wait).
    ///
    /// Use case: Claude sends one `execute_turn` command with the full
    /// turn intent (move target, ability + target, then wait). The bridge
    /// expands it into the same existing primitives already wired, but
    /// saves 6+ round trips per turn. TODO §1 Tier 5.
    /// </summary>
    public class TurnPlanTests
    {
        [Fact]
        public void EmptyPlan_EmitsJustWait()
        {
            // No move, no ability → still end the turn.
            var plan = new TurnPlan();
            var steps = plan.ToSteps().ToList();
            Assert.Single(steps);
            Assert.Equal("battle_wait", steps[0].Action);
        }

        [Fact]
        public void MoveOnly_EmitsMoveWithoutWait()
        {
            // Per Commands.md "DOES NOT END THE TURN" + BattleTurns.md
            // Move-only-as-reposition: caller wants to relocate without
            // committing the turn so they can rescan / decide their action
            // from the new position. battle_wait is appended only when
            // there's an actual action (ability) or genuinely-empty plan
            // (skip the turn). Live-flagged 2026-04-25 playtest: agent
            // expected `execute_turn 6 9` to leave Lloyd at (6,9) with
            // Act/Wait still available; instead his turn ended.
            var plan = new TurnPlan { MoveX = 6, MoveY = 5 };
            var steps = plan.ToSteps().ToList();
            Assert.Single(steps);
            Assert.Equal("battle_move", steps[0].Action);
            Assert.Equal(6, steps[0].X);
            Assert.Equal(5, steps[0].Y);
        }

        [Fact]
        public void MoveOnly_DirectionIgnored_NoWait()
        {
            // Direction is for the wait sub-step; with no wait, it has
            // nothing to attach to. Plan still emits move-only; the
            // direction is silently ignored (could log later if useful).
            var plan = new TurnPlan { MoveX = 6, MoveY = 5, Direction = "N" };
            var steps = plan.ToSteps().ToList();
            Assert.Single(steps);
            Assert.Equal("battle_move", steps[0].Action);
        }

        [Fact]
        public void AttackOnly_EmitsAttackThenWait()
        {
            var plan = new TurnPlan
            {
                AbilityName = "Attack",
                TargetX = 7,
                TargetY = 5,
            };
            var steps = plan.ToSteps().ToList();
            Assert.Equal(2, steps.Count);
            Assert.Equal("battle_ability", steps[0].Action);
            Assert.Equal("Attack", steps[0].AbilityName);
            Assert.Equal(7, steps[0].X);
            Assert.Equal(5, steps[0].Y);
            Assert.Equal("battle_wait", steps[1].Action);
        }

        [Fact]
        public void MoveAndAttack_EmitsMoveThenAttackThenWait()
        {
            var plan = new TurnPlan
            {
                MoveX = 6, MoveY = 5,
                AbilityName = "Attack",
                TargetX = 7, TargetY = 5,
            };
            var steps = plan.ToSteps().ToList();
            Assert.Equal(3, steps.Count);
            Assert.Equal("battle_move", steps[0].Action);
            Assert.Equal("battle_ability", steps[1].Action);
            Assert.Equal("battle_wait", steps[2].Action);
        }

        [Fact]
        public void SelfTargetAbility_HasNoCoordinates()
        {
            // Shout, Chakra, etc. — self-target abilities. TargetX/Y are
            // not set when AbilityName is present but targets are absent.
            var plan = new TurnPlan { AbilityName = "Shout" };
            var steps = plan.ToSteps().ToList();
            Assert.Equal(2, steps.Count);
            Assert.Equal("battle_ability", steps[0].Action);
            Assert.Equal("Shout", steps[0].AbilityName);
            Assert.False(steps[0].HasTarget);
            Assert.Equal("battle_wait", steps[1].Action);
        }

        [Fact]
        public void SkipWait_OmitsFinalStep()
        {
            // Advanced flow: some scripts want to inspect state between
            // move and ability, or between ability and wait. SkipWait=true
            // drops the trailing wait so the caller can inject calls.
            var plan = new TurnPlan
            {
                MoveX = 6, MoveY = 5,
                SkipWait = true,
            };
            var steps = plan.ToSteps().ToList();
            Assert.Single(steps);
            Assert.Equal("battle_move", steps[0].Action);
        }

        [Fact]
        public void WaitOnly_EmitsOneStep()
        {
            // Equivalent to the empty plan. Used to let a unit skip its
            // turn intentionally.
            var plan = new TurnPlan();
            var steps = plan.ToSteps().ToList();
            Assert.Single(steps);
            Assert.Equal("battle_wait", steps[0].Action);
        }

        [Fact]
        public void Direction_PropagatesToWait()
        {
            // Optional facing direction (N/S/E/W) for the trailing wait.
            var plan = new TurnPlan { Direction = "N" };
            var steps = plan.ToSteps().ToList();
            Assert.Single(steps);
            Assert.Equal("battle_wait", steps[0].Action);
            Assert.Equal("N", steps[0].Direction);
        }

        [Fact]
        public void AbilityWithoutName_IsIgnored()
        {
            // If TargetX/Y are set but AbilityName is null, don't emit a
            // bogus ability step. The targets without a name can't dispatch
            // anywhere meaningful.
            var plan = new TurnPlan
            {
                TargetX = 7, TargetY = 5,
            };
            var steps = plan.ToSteps().ToList();
            Assert.Single(steps);
            Assert.Equal("battle_wait", steps[0].Action);
        }
    }
}
