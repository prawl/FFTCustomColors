using System.Linq;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// Pure helper: enumerate tiles along a cardinal line from a caster to
// some extent. Line AoE abilities (Shockwave, Ice Saber, etc.) pick a
// direction and hit every tile along that axis up to a length limit.
//
// Shape rules:
//   - 4 cardinal directions (N/S/E/W) — no diagonals
//   - Line starts one tile past the caster; caster itself is NOT hit
//   - Range limit N means N tiles in the chosen direction
//   - Tiles are returned in order, closest first (useful for
//     "first enemy hit" lookups)
public class LineAoeCalculatorTests
{
    [Fact]
    public void East_RangeThree_ReturnsThreeTiles()
    {
        var tiles = LineAoeCalculator.Enumerate(
            casterX: 5, casterY: 5, direction: LineDirection.East, range: 3).ToList();
        Assert.Equal(3, tiles.Count);
        Assert.Equal((6, 5), tiles[0]);
        Assert.Equal((7, 5), tiles[1]);
        Assert.Equal((8, 5), tiles[2]);
    }

    [Fact]
    public void West_RangeTwo_ReturnsTwoTiles()
    {
        var tiles = LineAoeCalculator.Enumerate(
            casterX: 5, casterY: 5, direction: LineDirection.West, range: 2).ToList();
        Assert.Equal(2, tiles.Count);
        Assert.Equal((4, 5), tiles[0]);
        Assert.Equal((3, 5), tiles[1]);
    }

    [Fact]
    public void North_DecreasesY()
    {
        // Screen Y axis: North = smaller Y.
        var tiles = LineAoeCalculator.Enumerate(
            casterX: 5, casterY: 5, direction: LineDirection.North, range: 3).ToList();
        Assert.Equal((5, 4), tiles[0]);
        Assert.Equal((5, 3), tiles[1]);
        Assert.Equal((5, 2), tiles[2]);
    }

    [Fact]
    public void South_IncreasesY()
    {
        var tiles = LineAoeCalculator.Enumerate(
            casterX: 5, casterY: 5, direction: LineDirection.South, range: 2).ToList();
        Assert.Equal((5, 6), tiles[0]);
        Assert.Equal((5, 7), tiles[1]);
    }

    [Fact]
    public void Range_Zero_ReturnsEmpty()
    {
        var tiles = LineAoeCalculator.Enumerate(
            casterX: 5, casterY: 5, direction: LineDirection.East, range: 0).ToList();
        Assert.Empty(tiles);
    }

    [Fact]
    public void Range_Negative_ReturnsEmpty()
    {
        var tiles = LineAoeCalculator.Enumerate(
            casterX: 5, casterY: 5, direction: LineDirection.East, range: -1).ToList();
        Assert.Empty(tiles);
    }

    [Fact]
    public void AllDirections_ShareSameRangeCount()
    {
        const int range = 4;
        Assert.Equal(range, LineAoeCalculator.Enumerate(5, 5, LineDirection.North, range).Count());
        Assert.Equal(range, LineAoeCalculator.Enumerate(5, 5, LineDirection.South, range).Count());
        Assert.Equal(range, LineAoeCalculator.Enumerate(5, 5, LineDirection.East, range).Count());
        Assert.Equal(range, LineAoeCalculator.Enumerate(5, 5, LineDirection.West, range).Count());
    }

    [Fact]
    public void Caster_Not_Included()
    {
        // Caster's own tile is never in the line.
        var tiles = LineAoeCalculator.Enumerate(5, 5, LineDirection.East, 3).ToList();
        Assert.DoesNotContain((5, 5), tiles);
    }

    [Fact]
    public void TilesOrderedFromNearToFar()
    {
        // First tile in the list is always closest to caster.
        var east = LineAoeCalculator.Enumerate(5, 5, LineDirection.East, 5).ToList();
        for (int i = 1; i < east.Count; i++)
        {
            int prev = System.Math.Abs(east[i - 1].x - 5);
            int cur = System.Math.Abs(east[i].x - 5);
            Assert.True(cur > prev);
        }
    }

    [Fact]
    public void BestSeed_ByEnemyCount_PicksDirectionWithMostEnemies()
    {
        // Convenience: given a caster position and an enemy-tile set,
        // pick the cardinal direction that maximizes enemies hit for a
        // given range. Ties broken by North < South < East < West order.
        var enemies = new System.Collections.Generic.HashSet<(int x, int y)>
        {
            (6, 5), (7, 5), (8, 5), // East: 3 enemies
            (5, 4),                  // North: 1 enemy
            (4, 5),                  // West: 1 enemy
        };
        var best = LineAoeCalculator.PickBestDirection(
            casterX: 5, casterY: 5, range: 3, enemyTiles: enemies);
        Assert.Equal(LineDirection.East, best);
    }

    [Fact]
    public void BestSeed_NoEnemiesInAnyDirection_ReturnsNull()
    {
        var enemies = new System.Collections.Generic.HashSet<(int x, int y)>
        {
            (20, 20), // far away, no direction hits
        };
        var best = LineAoeCalculator.PickBestDirection(
            casterX: 5, casterY: 5, range: 3, enemyTiles: enemies);
        Assert.Null(best);
    }
}
