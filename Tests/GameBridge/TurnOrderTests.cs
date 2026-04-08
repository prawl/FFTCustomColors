using FFTColorCustomizer.GameBridge;
using Xunit;
using System.Collections.Generic;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class TurnOrderTests
    {
        [Fact]
        public void PredictTurnOrder_HigherSpeedActsFirst()
        {
            var units = new List<(string? name, string team, int ct, int speed)>
            {
                ("Ramza", "PLAYER", 10, 8),    // (100-10)/8 = 11.25 ticks
                ("Lloyd", "PLAYER", 10, 16),   // (100-10)/16 = 5.625 ticks
                (null, "ENEMY", 10, 10),        // (100-10)/10 = 9 ticks
            };

            var order = TurnOrderPredictor.Predict(units, maxTurns: 3);

            Assert.Equal(3, order.Count);
            Assert.Equal("Lloyd", order[0].Name);   // fastest
            Assert.Equal("ENEMY", order[1].Team);    // middle
            Assert.Equal("Ramza", order[2].Name);    // slowest
        }

        [Fact]
        public void PredictTurnOrder_HigherCTActsFirst_SameSpeed()
        {
            var units = new List<(string? name, string team, int ct, int speed)>
            {
                (null, "ENEMY", 50, 10),   // (100-50)/10 = 5 ticks
                (null, "PLAYER", 80, 10),  // (100-80)/10 = 2 ticks
            };

            var order = TurnOrderPredictor.Predict(units, maxTurns: 2);

            Assert.Equal("PLAYER", order[0].Team);  // closer to 100
            Assert.Equal("ENEMY", order[1].Team);
        }

        [Fact]
        public void PredictTurnOrder_FastUnitAppearsMultipleTimes()
        {
            var units = new List<(string? name, string team, int ct, int speed)>
            {
                ("Fast", "PLAYER", 90, 20),  // acts at tick 1, then every ~5 ticks
                ("Slow", "ENEMY", 0, 5),      // acts at tick 20
            };

            var order = TurnOrderPredictor.Predict(units, maxTurns: 5);

            // Fast laps Slow multiple times before Slow reaches 100
            Assert.Equal("Fast", order[0].Name);
            Assert.Equal("Fast", order[1].Name);
            Assert.Equal("Fast", order[2].Name);
            // Slow eventually acts
            Assert.Contains(order, e => e.Name == "Slow");
        }

        [Fact]
        public void PredictTurnOrder_ZeroSpeed_Excluded()
        {
            var units = new List<(string? name, string team, int ct, int speed)>
            {
                ("Active", "PLAYER", 50, 10),
                ("Dead", "ENEMY", 0, 0),  // dead/stopped, speed=0
            };

            var order = TurnOrderPredictor.Predict(units, maxTurns: 2);

            // Dead unit never appears in turn order
            Assert.All(order, e => Assert.Equal("Active", e.Name));
            Assert.DoesNotContain(order, e => e.Name == "Dead");
        }

        [Fact]
        public void PredictTurnOrder_EmptyList_ReturnsEmpty()
        {
            var units = new List<(string? name, string team, int ct, int speed)>();
            var order = TurnOrderPredictor.Predict(units, maxTurns: 5);
            Assert.Empty(order);
        }
    }
}
