using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// `Move tiles:` list rendered in scan_move responses is currently
    /// unsorted (BFS visit order). LLM agents have to mentally cross-ref
    /// against the unit list to pick a tile near an enemy. Sorting by min
    /// Manhattan distance to nearest enemy puts the most-actionable tiles
    /// at the front of the line. Live-flagged 2026-04-25 playtest.
    ///
    /// Tile metadata (height) is preserved; only the order changes.
    /// Stable sort so tied distances keep their BFS-visit order.
    /// </summary>
    public class MoveTileSorterTests
    {
        private static TilePosition T(int x, int y, double h = 0)
            => new() { X = x, Y = y, H = h };

        private static List<(int, int)> E(params (int, int)[] enemies)
            => new(enemies);

        [Fact]
        public void NoEnemies_PreservesOriginalOrder()
        {
            var tiles = new List<TilePosition> { T(1, 1), T(5, 5), T(3, 3) };
            var sorted = MoveTileSorter.SortByNearestEnemy(tiles, E());
            Assert.Equal(3, sorted.Count);
            Assert.Equal((1, 1), (sorted[0].X, sorted[0].Y));
            Assert.Equal((5, 5), (sorted[1].X, sorted[1].Y));
            Assert.Equal((3, 3), (sorted[2].X, sorted[2].Y));
        }

        [Fact]
        public void SingleEnemy_SortsAscendingByManhattanDistance()
        {
            // Enemy at (5,5). Distances:
            //   (4,5) = 1
            //   (5,5) = 0  (same tile — N/A in practice but valid input)
            //   (1,1) = 8
            //   (3,3) = 4
            var tiles = new List<TilePosition> { T(1, 1), T(4, 5), T(3, 3), T(5, 5) };
            var sorted = MoveTileSorter.SortByNearestEnemy(tiles, E((5, 5)));
            Assert.Equal((5, 5), (sorted[0].X, sorted[0].Y)); // d=0
            Assert.Equal((4, 5), (sorted[1].X, sorted[1].Y)); // d=1
            Assert.Equal((3, 3), (sorted[2].X, sorted[2].Y)); // d=4
            Assert.Equal((1, 1), (sorted[3].X, sorted[3].Y)); // d=8
        }

        [Fact]
        public void MultipleEnemies_UsesMinDistance()
        {
            // Enemies at (0,0) and (10,10).
            //   (1,1) = min(2,18) = 2
            //   (9,9) = min(18,2) = 2     (tie with (1,1) — stable order)
            //   (5,5) = min(10,10) = 10
            //   (0,3) = min(3,17) = 3
            var tiles = new List<TilePosition> { T(5, 5), T(1, 1), T(9, 9), T(0, 3) };
            var sorted = MoveTileSorter.SortByNearestEnemy(tiles, E((0, 0), (10, 10)));
            Assert.Equal((1, 1), (sorted[0].X, sorted[0].Y)); // d=2 first by stable order
            Assert.Equal((9, 9), (sorted[1].X, sorted[1].Y)); // d=2 second
            Assert.Equal((0, 3), (sorted[2].X, sorted[2].Y)); // d=3
            Assert.Equal((5, 5), (sorted[3].X, sorted[3].Y)); // d=10
        }

        [Fact]
        public void EmptyTiles_ReturnsEmpty()
        {
            var sorted = MoveTileSorter.SortByNearestEnemy(
                new List<TilePosition>(), E((1, 1)));
            Assert.Empty(sorted);
        }

        [Fact]
        public void StableSort_TiedDistancesKeepOriginalOrder()
        {
            // All tiles equidistant (d=1) from enemy at (5,5). Stable sort
            // preserves the BFS-visit order from input.
            var tiles = new List<TilePosition> { T(4, 5), T(5, 4), T(6, 5), T(5, 6) };
            var sorted = MoveTileSorter.SortByNearestEnemy(tiles, E((5, 5)));
            Assert.Equal((4, 5), (sorted[0].X, sorted[0].Y));
            Assert.Equal((5, 4), (sorted[1].X, sorted[1].Y));
            Assert.Equal((6, 5), (sorted[2].X, sorted[2].Y));
            Assert.Equal((5, 6), (sorted[3].X, sorted[3].Y));
        }

        [Fact]
        public void HeightFieldPreserved()
        {
            // Sort doesn't touch tile metadata — only order.
            var tiles = new List<TilePosition>
            {
                T(1, 1, 5.0),
                T(5, 5, 2.0),
            };
            var sorted = MoveTileSorter.SortByNearestEnemy(tiles, E((5, 5)));
            Assert.Equal((5, 5), (sorted[0].X, sorted[0].Y));
            Assert.Equal(2.0, sorted[0].H);
            Assert.Equal((1, 1), (sorted[1].X, sorted[1].Y));
            Assert.Equal(5.0, sorted[1].H);
        }
    }
}
