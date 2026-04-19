using FFTColorCustomizer.GameBridge;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for <see cref="TurnOrderPredictor.Predict"/>. Simulates CT
    /// accumulation tick-by-tick: each tick every unit gains Speed to CT;
    /// when CT ≥ 100 the unit acts and CT is reduced by 100. Pure algorithm
    /// with no prior test coverage.
    /// </summary>
    public class TurnOrderPredictorTests
    {
        [Fact]
        public void NoUnits_ReturnsEmpty()
        {
            var result = TurnOrderPredictor.Predict(
                new List<(string?, string, int, int)>());
            Assert.Empty(result);
        }

        [Fact]
        public void AllUnitsZeroSpeed_ReturnsEmpty()
        {
            // Dead / stopped units with Speed 0 never act.
            var units = new List<(string?, string, int, int)>
            {
                ("Dead", "PLAYER", 0, 0),
                ("Stopped", "PLAYER", 50, 0),
            };
            var result = TurnOrderPredictor.Predict(units);
            Assert.Empty(result);
        }

        [Fact]
        public void SingleUnit_Speed10_ActsEveryTenTicks()
        {
            // CT starts 0, gains 10 per tick, crosses 100 at tick 10.
            // After acting CT -= 100 → 0; next tick back to 10.
            // 9 turns over 90 ticks.
            var units = new List<(string?, string, int, int)>
            {
                ("A", "PLAYER", 0, 10),
            };
            var result = TurnOrderPredictor.Predict(units, maxTurns: 9);
            Assert.Equal(9, result.Count);
            Assert.All(result, e => Assert.Equal("A", e.Name));
        }

        [Fact]
        public void HigherSpeed_ActsMoreOften()
        {
            // Fast 50, Slow 10. Over ~20 ticks Fast should act ~10 times,
            // Slow ~2 times.
            var units = new List<(string?, string, int, int)>
            {
                ("Fast", "PLAYER", 0, 50),
                ("Slow", "ENEMY", 0, 10),
            };
            var result = TurnOrderPredictor.Predict(units, maxTurns: 12);
            var fastCount = result.Count(e => e.Name == "Fast");
            var slowCount = result.Count(e => e.Name == "Slow");
            Assert.True(fastCount > slowCount,
                $"Fast should act more than Slow. fast={fastCount} slow={slowCount}");
        }

        [Fact]
        public void HigherInitialCT_ActsFirst()
        {
            // Same Speed but A starts at CT=90 while B starts at CT=0.
            // A crosses 100 on tick 1 (90+10); B takes 10 ticks.
            var units = new List<(string?, string, int, int)>
            {
                ("A", "PLAYER", 90, 10),
                ("B", "ENEMY", 0, 10),
            };
            var result = TurnOrderPredictor.Predict(units, maxTurns: 3);
            Assert.Equal("A", result[0].Name);
        }

        [Fact]
        public void TieInTick_HigherCTGoesFirst()
        {
            // Both cross 100 same tick; the one with higher resulting CT
            // acts first per Predict's sort rule (Speed 50 ties → first
            // mover gets higher CT at act time).
            var units = new List<(string?, string, int, int)>
            {
                ("A", "PLAYER", 0, 100),  // acts exactly at tick 1 with CT=100
                ("B", "ENEMY", 20, 100),  // acts at tick 1 with CT=120
            };
            var result = TurnOrderPredictor.Predict(units, maxTurns: 2);
            // B's CT after tick1 is 120 vs A's 100 → B acts first.
            Assert.Equal("B", result[0].Name);
        }

        [Fact]
        public void MaxTurns_LimitsOutput()
        {
            var units = new List<(string?, string, int, int)>
            {
                ("A", "PLAYER", 0, 50),
                ("B", "ENEMY", 0, 50),
            };
            var result = TurnOrderPredictor.Predict(units, maxTurns: 3);
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void TeamAndNameCopiedIntoEntries()
        {
            var units = new List<(string?, string, int, int)>
            {
                ("Ramza", "PLAYER", 0, 50),
            };
            var result = TurnOrderPredictor.Predict(units, maxTurns: 1);
            Assert.Single(result);
            Assert.Equal("Ramza", result[0].Name);
            Assert.Equal("PLAYER", result[0].Team);
        }

        [Fact]
        public void CtOnActingIsReflectedInEntry()
        {
            // A unit with Speed 10 starting CT=0 crosses 100 at tick 10
            // with CT=100. The entry's CT should echo this value.
            var units = new List<(string?, string, int, int)>
            {
                ("A", "PLAYER", 0, 10),
            };
            var result = TurnOrderPredictor.Predict(units, maxTurns: 1);
            Assert.Single(result);
            Assert.Equal(100, result[0].CT);
        }
    }
}
