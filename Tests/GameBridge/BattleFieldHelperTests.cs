using FFTColorCustomizer.GameBridge;
using System.Collections.Generic;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for <see cref="BattleFieldHelper"/> — GetOccupiedPositions
    /// and AllEnemiesDefeated are battle-state predicates with no direct
    /// tests prior to session 47. Pure logic — no memory, no game.
    /// </summary>
    public class BattleFieldHelperTests
    {
        [Fact]
        public void GetOccupiedPositions_ExcludesActiveUnit()
        {
            var units = new List<(int x, int y, int team, int hp, bool isActive)>
            {
                (5, 5, 0, 100, true),   // active — excluded
                (4, 5, 0, 80, false),
                (7, 5, 1, 100, false),
            };
            var result = BattleFieldHelper.GetOccupiedPositions(units);
            Assert.Equal(2, result.Count);
            Assert.Contains((4, 5), result);
            Assert.Contains((7, 5), result);
            Assert.DoesNotContain((5, 5), result);
        }

        [Fact]
        public void GetOccupiedPositions_ExcludesInvalidPositions()
        {
            // Units with negative coordinates (stale / not-yet-placed) are
            // excluded. BFS should skip them as obstacles.
            var units = new List<(int x, int y, int team, int hp, bool isActive)>
            {
                (-1, -1, 1, 100, false),  // stale
                (3, 4, 0, 50, false),
            };
            var result = BattleFieldHelper.GetOccupiedPositions(units);
            Assert.Single(result);
            Assert.Contains((3, 4), result);
        }

        [Fact]
        public void GetOccupiedPositions_IncludesAllTeamsAndDead()
        {
            // Allies, enemies, dead (hp=0), guests — anything with a valid
            // position counts as an obstacle for BFS.
            var units = new List<(int x, int y, int team, int hp, bool isActive)>
            {
                (1, 1, 0, 100, false),
                (2, 2, 1, 100, false),
                (3, 3, 2, 100, false),
                (4, 4, 0, 0, false), // dead body still occupies the tile
            };
            var result = BattleFieldHelper.GetOccupiedPositions(units);
            Assert.Equal(4, result.Count);
        }

        [Fact]
        public void GetOccupiedPositions_Empty_ReturnsEmpty()
        {
            var units = new List<(int x, int y, int team, int hp, bool isActive)>();
            var result = BattleFieldHelper.GetOccupiedPositions(units);
            Assert.Empty(result);
        }

        // ---- AllEnemiesDefeated ----

        private static BattleUnitState Enemy(int hp = 100, string? lifeState = null, List<string>? statuses = null)
            => new() { Team = 1, Hp = hp, MaxHp = 100, LifeState = lifeState, Statuses = statuses };

        private static BattleUnitState Ally(int hp = 100)
            => new() { Team = 0, Hp = hp, MaxHp = 100 };

        [Fact]
        public void AllEnemiesDefeated_NoEnemies_ReturnsFalse()
        {
            // Edge case guard: empty enemy list is suspicious (likely data
            // issue), so return false rather than claiming victory.
            var units = new List<BattleUnitState> { Ally() };
            Assert.False(BattleFieldHelper.AllEnemiesDefeated(units));
        }

        [Fact]
        public void AllEnemiesDefeated_AllAlive_ReturnsFalse()
        {
            var units = new List<BattleUnitState> { Enemy(), Enemy() };
            Assert.False(BattleFieldHelper.AllEnemiesDefeated(units));
        }

        [Fact]
        public void AllEnemiesDefeated_AllZeroHp_ReturnsTrue()
        {
            var units = new List<BattleUnitState> { Enemy(hp: 0), Enemy(hp: 0) };
            Assert.True(BattleFieldHelper.AllEnemiesDefeated(units));
        }

        [Fact]
        public void AllEnemiesDefeated_OneAlive_ReturnsFalse()
        {
            var units = new List<BattleUnitState> { Enemy(hp: 0), Enemy(hp: 50) };
            Assert.False(BattleFieldHelper.AllEnemiesDefeated(units));
        }

        [Fact]
        public void AllEnemiesDefeated_Crystal_CountsAsDefeated()
        {
            var units = new List<BattleUnitState> { Enemy(hp: 0, lifeState: "crystal") };
            Assert.True(BattleFieldHelper.AllEnemiesDefeated(units));
        }

        [Fact]
        public void AllEnemiesDefeated_Treasure_CountsAsDefeated()
        {
            var units = new List<BattleUnitState> { Enemy(hp: 0, lifeState: "treasure") };
            Assert.True(BattleFieldHelper.AllEnemiesDefeated(units));
        }

        [Fact]
        public void AllEnemiesDefeated_Petrified_CountsAsDefeated()
        {
            // Petrify doesn't drop HP to 0 but the enemy is out of action.
            var units = new List<BattleUnitState>
            {
                Enemy(hp: 100, statuses: new List<string> { "Petrify" })
            };
            Assert.True(BattleFieldHelper.AllEnemiesDefeated(units));
        }

        [Fact]
        public void AllEnemiesDefeated_IgnoresAllyHp()
        {
            // All enemies dead, all allies also dead — game ends in victory
            // (though ally deaths are bad, the enemy-defeat condition fires).
            var units = new List<BattleUnitState>
            {
                Enemy(hp: 0),
                Ally(hp: 0),
                Ally(hp: 0),
            };
            Assert.True(BattleFieldHelper.AllEnemiesDefeated(units));
        }

        [Fact]
        public void AllEnemiesDefeated_MixedLifeStates_AllDown()
        {
            var units = new List<BattleUnitState>
            {
                Enemy(hp: 0, lifeState: "dead"),
                Enemy(hp: 0, lifeState: "crystal"),
                Enemy(hp: 100, statuses: new List<string> { "Petrify" }),
            };
            Assert.True(BattleFieldHelper.AllEnemiesDefeated(units));
        }
    }
}
