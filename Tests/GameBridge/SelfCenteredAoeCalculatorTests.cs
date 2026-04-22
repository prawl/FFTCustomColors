using System.Linq;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// Pure helper: enumerate tiles within a Manhattan-distance radius of
// a caster, optionally excluding the caster's own tile. Self-centered
// AoE abilities (Chakra, Cyclone, Purification, Wave Fist) hit a
// diamond-shape area around the caster.
//
// AoE radius rules:
//   - Radius 1 = 4 adjacent + (optional) self = 4 or 5 tiles
//   - Radius 2 = 12 or 13 tiles (diamond shape)
//   - Radius N = tiles where |dx|+|dy| <= N
//   - Chakra includes self (radius 2 includes caster — 13 tiles)
//   - Cyclone excludes self (radius 1 around caster, 4 tiles)
public class SelfCenteredAoeCalculatorTests
{
    [Fact]
    public void Radius_1_IncludingSelf_Returns5Tiles()
    {
        var tiles = SelfCenteredAoeCalculator.Enumerate(
            casterX: 5, casterY: 5, radius: 1, includeSelf: true).ToList();
        Assert.Equal(5, tiles.Count);
        Assert.Contains((5, 5), tiles);
        Assert.Contains((4, 5), tiles);
        Assert.Contains((6, 5), tiles);
        Assert.Contains((5, 4), tiles);
        Assert.Contains((5, 6), tiles);
    }

    [Fact]
    public void Radius_1_ExcludingSelf_Returns4Tiles()
    {
        var tiles = SelfCenteredAoeCalculator.Enumerate(
            casterX: 5, casterY: 5, radius: 1, includeSelf: false).ToList();
        Assert.Equal(4, tiles.Count);
        Assert.DoesNotContain((5, 5), tiles);
    }

    [Fact]
    public void Radius_2_IncludingSelf_Returns13TileDiamond()
    {
        var tiles = SelfCenteredAoeCalculator.Enumerate(
            casterX: 5, casterY: 5, radius: 2, includeSelf: true).ToList();
        // Manhattan radius 2 diamond: 1 + 4 + 8 = 13 tiles
        Assert.Equal(13, tiles.Count);
    }

    [Fact]
    public void Radius_2_ExcludingSelf_Returns12Tiles()
    {
        var tiles = SelfCenteredAoeCalculator.Enumerate(
            casterX: 5, casterY: 5, radius: 2, includeSelf: false).ToList();
        Assert.Equal(12, tiles.Count);
    }

    [Fact]
    public void Radius_Zero_IncludingSelf_ReturnsOnlyCaster()
    {
        var tiles = SelfCenteredAoeCalculator.Enumerate(
            casterX: 5, casterY: 5, radius: 0, includeSelf: true).ToList();
        Assert.Single(tiles);
        Assert.Equal((5, 5), tiles[0]);
    }

    [Fact]
    public void Radius_Zero_ExcludingSelf_ReturnsEmpty()
    {
        var tiles = SelfCenteredAoeCalculator.Enumerate(
            casterX: 5, casterY: 5, radius: 0, includeSelf: false).ToList();
        Assert.Empty(tiles);
    }

    [Fact]
    public void Radius_Negative_ReturnsEmpty()
    {
        var tiles = SelfCenteredAoeCalculator.Enumerate(
            casterX: 5, casterY: 5, radius: -1, includeSelf: true).ToList();
        Assert.Empty(tiles);
    }

    [Fact]
    public void TilesAreWithinManhattanRadius()
    {
        int radius = 3;
        var tiles = SelfCenteredAoeCalculator.Enumerate(5, 5, radius, includeSelf: true);
        foreach (var (x, y) in tiles)
        {
            int manhattan = System.Math.Abs(x - 5) + System.Math.Abs(y - 5);
            Assert.True(manhattan <= radius,
                $"({x},{y}) has Manhattan={manhattan}, expected ≤{radius}");
        }
    }

    [Fact]
    public void FullField_RadiusNinetyNine_LargeButFinite()
    {
        // Bard/Dancer songs have AoE=99 (full-field). Enumerator caps at
        // radius 99 and returns a bounded list (no infinite loop).
        var tiles = SelfCenteredAoeCalculator.Enumerate(
            casterX: 5, casterY: 5, radius: 99, includeSelf: true).ToList();
        // 99 diamond = 1 + 4*(1+2+...+99) = 1 + 4*99*100/2 = 19801 tiles
        Assert.Equal(19801, tiles.Count);
    }

    [Fact]
    public void CountEnemiesHit_FiltersToHeldEnemies()
    {
        var enemies = new System.Collections.Generic.HashSet<(int x, int y)>
        {
            (5, 5),  // caster tile — won't count
            (4, 5),  // adjacent, in radius 1
            (6, 5),  // adjacent, in radius 1
            (5, 7),  // distance 2, in radius 2
            (10, 10), // far, not in radius
        };
        int hitR1 = SelfCenteredAoeCalculator.CountEnemiesHit(
            casterX: 5, casterY: 5, radius: 1, includeSelf: false, enemyTiles: enemies);
        Assert.Equal(2, hitR1);

        int hitR2 = SelfCenteredAoeCalculator.CountEnemiesHit(
            casterX: 5, casterY: 5, radius: 2, includeSelf: false, enemyTiles: enemies);
        Assert.Equal(3, hitR2); // (4,5), (6,5), (5,7)
    }
}
