using FFTColorCustomizer.GameBridge;
using System.Collections.Generic;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for <see cref="FacingDecider.Decide"/> — combines the explicit
    /// user-override path with the auto-pick path (FacingStrategy) into one
    /// pure function. Returns a <see cref="FacingDecision"/> containing the
    /// (dx,dy) pair, cardinal name, arc counts, and a flag indicating
    /// whether the override was used.
    ///
    /// Session 47: unifies the inline ?? fallback scattered across
    /// NavigationActions.BattleWait + scan_move's RecommendedFacing.
    /// </summary>
    public class FacingDeciderTests
    {
        private static FacingStrategy.UnitPosition Ally(int x, int y)
            => new() { GridX = x, GridY = y, Team = 0, Hp = 100, MaxHp = 100 };

        private static FacingStrategy.UnitPosition Enemy(int x, int y)
            => new() { GridX = x, GridY = y, Team = 1, Hp = 100, MaxHp = 100 };

        [Fact]
        public void NoOverride_NoEnemies_ReturnsDefaultFacing()
        {
            var decision = FacingDecider.Decide(
                facingOverride: null,
                ally: Ally(5, 5),
                enemies: new List<FacingStrategy.UnitPosition>());
            Assert.False(decision.FromOverride);
            // With no enemies, FacingStrategy returns a default. Just verify
            // the decision is well-formed (cardinal name populated).
            Assert.NotNull(decision.DirectionName);
            Assert.NotEmpty(decision.DirectionName);
        }

        [Fact]
        public void Override_East_UsesExactly()
        {
            var decision = FacingDecider.Decide(
                facingOverride: (1, 0),
                ally: Ally(5, 5),
                enemies: new List<FacingStrategy.UnitPosition> { Enemy(0, 5) });
            Assert.True(decision.FromOverride);
            Assert.Equal(1, decision.Dx);
            Assert.Equal(0, decision.Dy);
            Assert.Equal("East", decision.DirectionName);
        }

        [Fact]
        public void Override_North_UsesExactly()
        {
            var decision = FacingDecider.Decide(
                facingOverride: (0, 1),
                ally: Ally(5, 5),
                enemies: new List<FacingStrategy.UnitPosition>());
            Assert.True(decision.FromOverride);
            Assert.Equal("North", decision.DirectionName);
        }

        [Fact]
        public void Override_South_UsesExactly()
        {
            var decision = FacingDecider.Decide(
                facingOverride: (0, -1),
                ally: Ally(5, 5),
                enemies: new List<FacingStrategy.UnitPosition>());
            Assert.Equal("South", decision.DirectionName);
        }

        [Fact]
        public void Override_West_UsesExactly()
        {
            var decision = FacingDecider.Decide(
                facingOverride: (-1, 0),
                ally: Ally(5, 5),
                enemies: new List<FacingStrategy.UnitPosition>());
            Assert.Equal("West", decision.DirectionName);
        }

        [Fact]
        public void AutoPick_EnemyToEast_FacesEast()
        {
            // Single enemy 3 tiles east of ally. FacingStrategy should put
            // the enemy in the ally's front arc → face East.
            var decision = FacingDecider.Decide(
                facingOverride: null,
                ally: Ally(5, 5),
                enemies: new List<FacingStrategy.UnitPosition> { Enemy(8, 5) });
            Assert.False(decision.FromOverride);
            Assert.Equal("East", decision.DirectionName);
            Assert.Equal(1, decision.Front);
        }

        [Fact]
        public void AutoPick_EnemyToNorth_FacesNorth()
        {
            var decision = FacingDecider.Decide(
                facingOverride: null,
                ally: Ally(5, 5),
                enemies: new List<FacingStrategy.UnitPosition> { Enemy(5, 8) });
            Assert.False(decision.FromOverride);
            Assert.Equal("North", decision.DirectionName);
        }

        [Fact]
        public void Override_DoesNotCallAutoPick_EvenWithEnemies()
        {
            // Override takes precedence even when auto-pick would choose
            // differently. Two enemies to the East; user overrides West.
            var decision = FacingDecider.Decide(
                facingOverride: (-1, 0),
                ally: Ally(5, 5),
                enemies: new List<FacingStrategy.UnitPosition>
                {
                    Enemy(8, 5),
                    Enemy(9, 5),
                });
            Assert.True(decision.FromOverride);
            Assert.Equal("West", decision.DirectionName);
        }

        [Fact]
        public void AutoPick_ArcCounts_Populated()
        {
            // With several enemies, front/side/back counts should sum to the
            // total enemy count (each enemy lands in exactly one arc).
            var enemies = new List<FacingStrategy.UnitPosition>
            {
                Enemy(8, 5), Enemy(3, 5), Enemy(5, 8), Enemy(5, 2),
            };
            var decision = FacingDecider.Decide(
                facingOverride: null,
                ally: Ally(5, 5),
                enemies: enemies);
            Assert.Equal(enemies.Count, decision.Front + decision.Side + decision.Back);
        }

        [Fact]
        public void Nonstandard_OverridePair_FormatsAsDxDy()
        {
            // An override that isn't a cardinal (shouldn't happen in practice,
            // but guard the name formatting). Output is "(dx,dy)" fallback.
            var decision = FacingDecider.Decide(
                facingOverride: (1, 1),
                ally: Ally(5, 5),
                enemies: new List<FacingStrategy.UnitPosition>());
            Assert.Equal("(1,1)", decision.DirectionName);
        }
    }
}
