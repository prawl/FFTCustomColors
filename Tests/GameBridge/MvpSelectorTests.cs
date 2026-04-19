using FFTColorCustomizer.GameBridge;
using System.Collections.Generic;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for <see cref="MvpSelector.Select"/> — pure MVP picker for
    /// battle-end stats. Score formula extracted from the inline block in
    /// <see cref="BattleStatTracker"/>:
    ///
    ///   score = kills * 300 + damageDealt + healingDealt / 2 - timesKOd * 200
    ///
    /// Session 47: extracting the scoring so it gets dedicated test coverage.
    /// Previously only indirectly tested through BattleStatTracker tests.
    /// </summary>
    public class MvpSelectorTests
    {
        private static UnitBattleStats Unit(
            int kills = 0, int damageDealt = 0,
            int healingDealt = 0, int timesKOd = 0)
            => new()
            {
                Kills = kills,
                DamageDealt = damageDealt,
                HealingDealt = healingDealt,
                TimesKOd = timesKOd,
            };

        [Fact]
        public void EmptyStats_ReturnsNull()
        {
            var mvp = MvpSelector.Select(new Dictionary<string, UnitBattleStats>());
            Assert.Null(mvp);
        }

        [Fact]
        public void SingleUnit_IsAlwaysMvp()
        {
            var stats = new Dictionary<string, UnitBattleStats>
            {
                ["Ramza"] = Unit(damageDealt: 10),
            };
            Assert.Equal("Ramza", MvpSelector.Select(stats));
        }

        [Fact]
        public void HighestDamage_WinsWhenNoOtherSignals()
        {
            var stats = new Dictionary<string, UnitBattleStats>
            {
                ["A"] = Unit(damageDealt: 100),
                ["B"] = Unit(damageDealt: 250),
                ["C"] = Unit(damageDealt: 50),
            };
            Assert.Equal("B", MvpSelector.Select(stats));
        }

        [Fact]
        public void Kills_OutweighPlainDamage()
        {
            // 1 kill = 300 points. Unit B deals 250 dmg but no kills;
            // unit A has 1 kill + 50 dmg = 350 score. A wins.
            var stats = new Dictionary<string, UnitBattleStats>
            {
                ["A"] = Unit(kills: 1, damageDealt: 50),
                ["B"] = Unit(damageDealt: 250),
            };
            Assert.Equal("A", MvpSelector.Select(stats));
        }

        [Fact]
        public void TimesKOd_PenalizesScore()
        {
            // 1 KO = -200. A does 300 dmg but KO'd twice (-400) = -100.
            // B does 150 dmg with no KO = 150. B wins.
            var stats = new Dictionary<string, UnitBattleStats>
            {
                ["A"] = Unit(damageDealt: 300, timesKOd: 2),
                ["B"] = Unit(damageDealt: 150),
            };
            Assert.Equal("B", MvpSelector.Select(stats));
        }

        [Fact]
        public void HealingCounts_AtHalfWeight()
        {
            // Healing worth 0.5× damage. A heals 200 = 100 score. B deals
            // 99 = 99 score. A wins by 1.
            var stats = new Dictionary<string, UnitBattleStats>
            {
                ["A"] = Unit(healingDealt: 200),
                ["B"] = Unit(damageDealt: 99),
            };
            Assert.Equal("A", MvpSelector.Select(stats));
        }

        [Fact]
        public void Ties_FirstInsertionWins()
        {
            // Both score 100. Dictionary iteration order = insertion.
            var stats = new Dictionary<string, UnitBattleStats>
            {
                ["First"] = Unit(damageDealt: 100),
                ["Second"] = Unit(damageDealt: 100),
            };
            Assert.Equal("First", MvpSelector.Select(stats));
        }

        [Fact]
        public void AllZeroes_StillPicks_OneUnit()
        {
            // All units have 0 score — degenerate but valid (trivial battle
            // where nothing happened). Picks the first inserted.
            var stats = new Dictionary<string, UnitBattleStats>
            {
                ["Ramza"] = Unit(),
                ["Agrias"] = Unit(),
            };
            Assert.Equal("Ramza", MvpSelector.Select(stats));
        }

        [Fact]
        public void Score_Formula_Matches()
        {
            // Expose the score directly so the formula can be unit-tested.
            // 2 kills + 150 dmg + 40 healing (= 20) - 1 KO (= -200)
            //   = 600 + 150 + 20 - 200 = 570.
            Assert.Equal(570, MvpSelector.Score(Unit(
                kills: 2, damageDealt: 150, healingDealt: 40, timesKOd: 1)));
        }

        [Fact]
        public void Score_ZeroedInput_IsZero()
        {
            Assert.Equal(0, MvpSelector.Score(Unit()));
        }
    }
}
