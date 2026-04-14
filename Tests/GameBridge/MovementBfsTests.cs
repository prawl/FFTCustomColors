using FFTColorCustomizer.GameBridge;
using FFTColorCustomizer.Utilities;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class MovementBfsTests
    {
        /// <summary>
        /// Ally-occupied tiles can be walked through but cost +1 move point
        /// and cannot be a final destination. Verified in-game 2026-04-13:
        /// Kenrick at (10,9) Move=4 Jump=4 with allies at (9,9), (10,10), (10,6).
        /// Without ally penalty: BFS returns 13 tiles (3 extras past allies).
        /// With ally penalty: BFS returns exactly the 10 game-valid tiles.
        /// </summary>
        [Fact]
        public void BFS_AllyTiles_CostExtraAndCannotBeDestination()
        {
            // Flat 3x3 map, all height 0, no obstacles
            var map = CreateFlatMap(5, 5, tileHeight: 0);

            // Unit at (2,2), Move=2, Jump=4
            // Ally at (2,3) — directly adjacent
            var allyPositions = new HashSet<(int, int)> { (2, 3) };

            var tiles = MovementBfs.ComputeValidTiles(
                map, unitX: 2, unitY: 2, moveStat: 2, jumpStat: 4,
                enemyPositions: null, allyPositions: allyPositions);

            var tileSet = tiles.Select(t => (t.X, t.Y)).ToHashSet();

            // Ally tile (2,3) should NOT be in the result (can't stop on ally)
            Assert.DoesNotContain((2, 3), tileSet);

            // (2,4) is 2 tiles away normally, but through ally at (2,3) costs 1+2=3 > Move=2
            // So (2,4) should NOT be reachable
            Assert.DoesNotContain((2, 4), tileSet);

            // (1,2), (3,2), (2,1) should all be reachable (no ally in path, cost=1)
            Assert.Contains((1, 2), tileSet);
            Assert.Contains((3, 2), tileSet);
            Assert.Contains((2, 1), tileSet);

            // (0,2), (4,2), (2,0) should be reachable (cost=2, no ally in path)
            Assert.Contains((0, 2), tileSet);
            Assert.Contains((4, 2), tileSet);
            Assert.Contains((2, 0), tileSet);
        }

        [Theory]
        [InlineData("Movement +1", 4, 4, 5, 4)]
        [InlineData("Movement +2", 4, 4, 6, 4)]
        [InlineData("Movement +3", 4, 4, 7, 4)]
        [InlineData("Jump +1", 4, 3, 4, 4)]
        [InlineData("Jump +2", 4, 3, 4, 5)]
        [InlineData("Jump +3", 4, 3, 4, 6)]
        [InlineData("Teleport", 4, 3, 4, 3)]
        [InlineData("Fly", 4, 3, 4, 3)]
        [InlineData("Manafont", 4, 3, 4, 3)]
        [InlineData(null, 4, 3, 4, 3)]
        public void ApplyMovementAbility_CorrectBonus(string? abilityName, int baseMove, int baseJump, int expectedMove, int expectedJump)
        {
            var (move, jump) = MovementBfs.ApplyMovementAbility(baseMove, baseJump, abilityName);
            Assert.Equal(expectedMove, move);
            Assert.Equal(expectedJump, jump);
        }

        private static MapData CreateFlatMap(int width, int height, int tileHeight)
        {
            var map = new MapData
            {
                Width = width,
                Height = height,
                Tiles = new MapTile[width, height]
            };
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    map.Tiles[x, y] = new MapTile
                    {
                        Height = tileHeight,
                        SlopeHeight = 0,
                        Depth = 0,
                        NoWalk = false,
                        NoCursor = false,
                        SurfaceType = "Grassland",
                        SlopeType = "Flat 0"
                    };
            return map;
        }
    }
}
