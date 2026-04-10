using FFTColorCustomizer.GameBridge;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class OccupiedTilesTests
    {
        [Fact]
        public void GetOccupiedPositions_ExcludesActiveUnit()
        {
            var units = new List<(int x, int y, int team, int hp, bool isActive)>
            {
                (5, 9, 0, 496, true),  // active unit — should be excluded
                (6, 9, 0, 475, false), // ally
                (3, 8, 1, 400, false), // enemy
            };

            var result = BattleFieldHelper.GetOccupiedPositions(units);

            Assert.DoesNotContain((5, 9), result);
            Assert.Contains((6, 9), result);
            Assert.Contains((3, 8), result);
        }

        [Fact]
        public void GetOccupiedPositions_IncludesDeadUnits()
        {
            var units = new List<(int x, int y, int team, int hp, bool isActive)>
            {
                (5, 9, 0, 496, true),  // active
                (4, 9, 0, 0, false),   // dead ally — still occupies tile
                (3, 8, 1, 0, false),   // dead enemy — still occupies tile
            };

            var result = BattleFieldHelper.GetOccupiedPositions(units);

            Assert.Contains((4, 9), result);
            Assert.Contains((3, 8), result);
        }

        [Fact]
        public void GetOccupiedPositions_IncludesAllies()
        {
            var units = new List<(int x, int y, int team, int hp, bool isActive)>
            {
                (5, 9, 0, 496, true),  // active
                (6, 9, 0, 475, false), // ally
                (7, 9, 2, 200, false), // guest/NPC
            };

            var result = BattleFieldHelper.GetOccupiedPositions(units);

            Assert.Contains((6, 9), result);
            Assert.Contains((7, 9), result);
        }

        [Fact]
        public void GetOccupiedPositions_ExcludesInvalidPositions()
        {
            var units = new List<(int x, int y, int team, int hp, bool isActive)>
            {
                (5, 9, 0, 496, true),
                (-1, -1, 2, 134, false), // unknown position — exclude
            };

            var result = BattleFieldHelper.GetOccupiedPositions(units);

            Assert.DoesNotContain((-1, -1), result);
        }
    }
}
