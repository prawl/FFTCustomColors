using FFTColorCustomizer.GameBridge;
using System.Collections.Generic;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class BattleVictoryTests
    {
        [Fact]
        public void AllEnemiesDead_ReturnsTrue()
        {
            var units = new List<BattleUnitState>
            {
                new() { Team = 0, Hp = 500, MaxHp = 500 }, // player alive
                new() { Team = 1, Hp = 0, MaxHp = 400, LifeState = "dead" }, // enemy dead
                new() { Team = 1, Hp = 0, MaxHp = 300, LifeState = "dead" }, // enemy dead
            };

            Assert.True(BattleFieldHelper.AllEnemiesDefeated(units));
        }

        [Fact]
        public void OneEnemyAlive_ReturnsFalse()
        {
            var units = new List<BattleUnitState>
            {
                new() { Team = 0, Hp = 500, MaxHp = 500 },
                new() { Team = 1, Hp = 0, MaxHp = 400, LifeState = "dead" },
                new() { Team = 1, Hp = 100, MaxHp = 300 }, // still alive
            };

            Assert.False(BattleFieldHelper.AllEnemiesDefeated(units));
        }

        [Fact]
        public void NoEnemies_ReturnsFalse()
        {
            // No enemies at all shouldn't count as victory (edge case / data issue)
            var units = new List<BattleUnitState>
            {
                new() { Team = 0, Hp = 500, MaxHp = 500 },
            };

            Assert.False(BattleFieldHelper.AllEnemiesDefeated(units));
        }

        [Fact]
        public void CrystalAndTreasure_CountAsDefeated()
        {
            var units = new List<BattleUnitState>
            {
                new() { Team = 0, Hp = 500, MaxHp = 500 },
                new() { Team = 1, Hp = 0, MaxHp = 400, LifeState = "crystal" },
                new() { Team = 1, Hp = 0, MaxHp = 300, LifeState = "treasure" },
            };

            Assert.True(BattleFieldHelper.AllEnemiesDefeated(units));
        }

        [Fact]
        public void PetrifiedEnemy_CountsAsDefeated()
        {
            var units = new List<BattleUnitState>
            {
                new() { Team = 0, Hp = 500, MaxHp = 500 },
                new() { Team = 1, Hp = 0, MaxHp = 400, LifeState = "dead" },
                new() { Team = 1, Hp = 300, MaxHp = 300, Statuses = new List<string> { "Petrify" } },
            };

            Assert.True(BattleFieldHelper.AllEnemiesDefeated(units));
        }

        [Fact]
        public void EmptyList_ReturnsFalse()
        {
            Assert.False(BattleFieldHelper.AllEnemiesDefeated(new List<BattleUnitState>()));
        }
    }
}
