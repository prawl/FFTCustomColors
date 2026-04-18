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

        // Additional edge cases (session 33 batch 5).

        [Fact]
        public void NeutralTeam_DoesNotCountAsEnemy()
        {
            // Team 2 is neutral/NPC (guest allies). They shouldn't gate victory even
            // if alive — defeat is judged only on team=1.
            var units = new List<BattleUnitState>
            {
                new() { Team = 0, Hp = 500, MaxHp = 500 }, // player
                new() { Team = 1, Hp = 0, MaxHp = 400, LifeState = "dead" },
                new() { Team = 2, Hp = 300, MaxHp = 300 }, // neutral, still alive
            };
            Assert.True(BattleFieldHelper.AllEnemiesDefeated(units));
        }

        [Fact]
        public void AllEnemiesPetrified_ReturnsTrue()
        {
            var units = new List<BattleUnitState>
            {
                new() { Team = 0, Hp = 500, MaxHp = 500 },
                new() { Team = 1, Hp = 100, MaxHp = 400, Statuses = new List<string> { "Petrify" } },
                new() { Team = 1, Hp = 50, MaxHp = 300, Statuses = new List<string> { "Petrify" } },
            };
            Assert.True(BattleFieldHelper.AllEnemiesDefeated(units));
        }

        [Fact]
        public void MultipleEnemiesAlive_ReturnsFalse()
        {
            var units = new List<BattleUnitState>
            {
                new() { Team = 0, Hp = 500, MaxHp = 500 },
                new() { Team = 1, Hp = 100, MaxHp = 400 },
                new() { Team = 1, Hp = 50, MaxHp = 300 },
                new() { Team = 1, Hp = 200, MaxHp = 400 },
            };
            Assert.False(BattleFieldHelper.AllEnemiesDefeated(units));
        }

        [Fact]
        public void OnlyPlayerUnits_ReturnsFalse()
        {
            // Edge case: battle data where no enemies were loaded (pre-spawn or data issue).
            var units = new List<BattleUnitState>
            {
                new() { Team = 0, Hp = 500, MaxHp = 500 },
                new() { Team = 0, Hp = 400, MaxHp = 400 },
            };
            Assert.False(BattleFieldHelper.AllEnemiesDefeated(units));
        }

        [Fact]
        public void OnlyNeutralUnits_ReturnsFalse()
        {
            // No enemies, only guests. Can't "win" because there's no one to defeat.
            var units = new List<BattleUnitState>
            {
                new() { Team = 2, Hp = 500, MaxHp = 500 },
                new() { Team = 2, Hp = 400, MaxHp = 400 },
            };
            Assert.False(BattleFieldHelper.AllEnemiesDefeated(units));
        }

        [Fact]
        public void EnemyHp0ButLifeStateAlive_CountsAsDefeated()
        {
            // Safety: if HP is 0 we call it defeated regardless of LifeState string.
            // (Data may race during turn transitions.)
            var units = new List<BattleUnitState>
            {
                new() { Team = 0, Hp = 500, MaxHp = 500 },
                new() { Team = 1, Hp = 0, MaxHp = 400, LifeState = null },
            };
            Assert.True(BattleFieldHelper.AllEnemiesDefeated(units));
        }

        [Fact]
        public void MixedDefeatStates_AllVariants()
        {
            // Dead, crystal, treasure, petrified — every defeat variant in one battle.
            var units = new List<BattleUnitState>
            {
                new() { Team = 0, Hp = 500, MaxHp = 500 },
                new() { Team = 1, Hp = 0, MaxHp = 400, LifeState = "dead" },
                new() { Team = 1, Hp = 0, MaxHp = 300, LifeState = "crystal" },
                new() { Team = 1, Hp = 0, MaxHp = 200, LifeState = "treasure" },
                new() { Team = 1, Hp = 100, MaxHp = 350, Statuses = new List<string> { "Petrify" } },
            };
            Assert.True(BattleFieldHelper.AllEnemiesDefeated(units));
        }

        [Fact]
        public void SingleEnemyAlive_AmongDefeated_ReturnsFalse()
        {
            var units = new List<BattleUnitState>
            {
                new() { Team = 0, Hp = 500, MaxHp = 500 },
                new() { Team = 1, Hp = 0, MaxHp = 400, LifeState = "dead" },
                new() { Team = 1, Hp = 0, MaxHp = 300, LifeState = "crystal" },
                new() { Team = 1, Hp = 1, MaxHp = 200 }, // just barely alive
            };
            Assert.False(BattleFieldHelper.AllEnemiesDefeated(units));
        }
    }
}
