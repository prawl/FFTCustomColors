using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class CursorFloodFillTests
    {
        [Fact]
        public void Flood_SingleValidStart_ReturnsJustStartTile()
        {
            // Only (5,5) is valid — all neighbors invalid.
            var validTiles = new HashSet<(int, int)> { (5, 5) };
            bool isValid((int x, int y) t) => validTiles.Contains(t);

            var result = CursorFloodFill.Flood(startX: 5, startY: 5, isValid: isValid);

            Assert.Single(result);
            Assert.Contains((5, 5), result);
        }

        [Fact]
        public void Flood_CardinalNeighborsValid_ReturnsFiveTiles()
        {
            // Plus-shaped valid region centered on (5,5).
            var validTiles = new HashSet<(int, int)>
            {
                (5, 5), (4, 5), (6, 5), (5, 4), (5, 6)
            };
            bool isValid((int x, int y) t) => validTiles.Contains(t);

            var result = CursorFloodFill.Flood(5, 5, isValid);

            Assert.Equal(5, result.Count);
            foreach (var t in validTiles) Assert.Contains(t, result);
        }

        [Fact]
        public void Flood_WallBreaksExpansion_DoesNotReachIsolatedValidTile()
        {
            // (5,5) valid, (6,5) INVALID (wall), (7,5) valid but unreachable via cardinal path.
            var validTiles = new HashSet<(int, int)> { (5, 5), (7, 5) };
            bool isValid((int x, int y) t) => validTiles.Contains(t);

            var result = CursorFloodFill.Flood(5, 5, isValid);

            Assert.Single(result);
            Assert.Contains((5, 5), result);
            Assert.DoesNotContain((7, 5), result);
        }

        [Fact]
        public void Flood_LShapedRegion_AllReached()
        {
            // L-shape: (5,5), (5,6), (5,7), (6,7), (7,7)
            var validTiles = new HashSet<(int, int)>
            {
                (5, 5), (5, 6), (5, 7), (6, 7), (7, 7)
            };
            bool isValid((int x, int y) t) => validTiles.Contains(t);

            var result = CursorFloodFill.Flood(5, 5, isValid);

            Assert.Equal(5, result.Count);
        }

        [Fact]
        public void Flood_StartInvalid_ReturnsEmpty()
        {
            // If the start itself isn't valid, return empty (nothing to expand from).
            bool isValid((int x, int y) t) => false;
            var result = CursorFloodFill.Flood(5, 5, isValid);
            Assert.Empty(result);
        }

        [Fact]
        public void Flood_RespectsBoundsViaPredicateOnly()
        {
            // The predicate is the only source of truth; flood doesn't enforce its own bounds.
            // If predicate says (-1, -1) is valid, we explore it.
            var validTiles = new HashSet<(int, int)> { (0, 0), (-1, 0), (0, -1) };
            bool isValid((int x, int y) t) => validTiles.Contains(t);

            var result = CursorFloodFill.Flood(0, 0, isValid);

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void Flood_DiamondPattern_ReachesAllConnectedValidTiles()
        {
            // Diamond within a Move=2 Manhattan distance from (5,5).
            var validTiles = new HashSet<(int, int)>
            {
                (5, 5),
                (4, 5), (6, 5), (5, 4), (5, 6),
                (3, 5), (7, 5), (5, 3), (5, 7),
                (4, 4), (4, 6), (6, 4), (6, 6)
            };
            bool isValid((int x, int y) t) => validTiles.Contains(t);

            var result = CursorFloodFill.Flood(5, 5, isValid);

            Assert.Equal(13, result.Count);
        }

        [Fact]
        public void Flood_DoesNotCallIsValidTwicePerTile()
        {
            // Flood should memoize visits — each tile's predicate called at most once.
            var callCounts = new Dictionary<(int, int), int>();
            bool isValid((int x, int y) t)
            {
                callCounts[t] = (callCounts.GetValueOrDefault(t, 0)) + 1;
                return t == (5, 5) || t == (6, 5) || t == (5, 6) || t == (7, 5);
            }

            CursorFloodFill.Flood(5, 5, isValid);

            foreach (var kv in callCounts)
            {
                Assert.True(kv.Value <= 1, $"Tile {kv.Key} was probed {kv.Value} times");
            }
        }
    }
}
