using System.Collections.Generic;
using System.Linq;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge;

// Pure helper: given a caster position, ability range, and a list of
// candidate enemy tiles in range, score how the multi-hit ability
// distributes its N strikes across them.
//
// Multi-hit abilities (Ramuh, Shiva, Bio, some Monk abilities) fire
// multiple hits within an AoE; each hit targets a random in-range
// tile. This helper doesn't pick actual random strikes — it scores
// the CENTER tile that maximizes expected hits on enemies vs allies,
// letting scan_move rank targeting choices.
//
// Scoring heuristic: for each candidate center, count enemies in
// the AoE radius from that center. Expected-enemies-hit = enemy count
// × (hits / total-tiles-in-radius). Higher is better.
public class MultiHitTargetEnumeratorTests
{
    [Fact]
    public void NoEnemies_AllCentersScoreZero()
    {
        var enemies = new HashSet<(int x, int y)>();
        var result = MultiHitTargetEnumerator.Score(
            candidateCenter: (5, 5), aoeRadius: 2, hits: 3, enemyTiles: enemies, allyTiles: new HashSet<(int x, int y)>());
        Assert.Equal(0, result.Enemies);
        Assert.Equal(0, result.Allies);
    }

    [Fact]
    public void Center_WithAllEnemiesInRadius_CountsAll()
    {
        var enemies = new HashSet<(int x, int y)>
        {
            (5, 5), (4, 5), (6, 5), (5, 4), (5, 6),
        };
        var result = MultiHitTargetEnumerator.Score(
            candidateCenter: (5, 5), aoeRadius: 1, hits: 3, enemyTiles: enemies, allyTiles: new HashSet<(int x, int y)>());
        Assert.Equal(5, result.Enemies);
    }

    [Fact]
    public void Center_WithAllyNearby_CountsAllies()
    {
        var enemies = new HashSet<(int x, int y)> { (5, 5) };
        var allies = new HashSet<(int x, int y)> { (6, 5), (5, 6) };
        var result = MultiHitTargetEnumerator.Score(
            candidateCenter: (5, 5), aoeRadius: 1, hits: 3, enemyTiles: enemies, allyTiles: allies);
        Assert.Equal(1, result.Enemies);
        Assert.Equal(2, result.Allies);
    }

    [Fact]
    public void Net_IsEnemiesMinusAllies()
    {
        var enemies = new HashSet<(int x, int y)> { (5, 5), (4, 5), (6, 5) };
        var allies = new HashSet<(int x, int y)> { (5, 4) };
        var result = MultiHitTargetEnumerator.Score(
            candidateCenter: (5, 5), aoeRadius: 1, hits: 3, enemyTiles: enemies, allyTiles: allies);
        Assert.Equal(3 - 1, result.Net);
    }

    [Fact]
    public void RankCenters_SortsByNetDescending()
    {
        var enemies = new HashSet<(int x, int y)>
        {
            (10, 10), // Cluster A: isolated
            (5, 5), (4, 5), (6, 5), // Cluster B: 3 enemies cluster
        };
        var allies = new HashSet<(int x, int y)>();
        var candidates = new[] { (10, 10), (5, 5), (2, 2) };
        var ranked = MultiHitTargetEnumerator.RankCenters(
            candidates, aoeRadius: 1, hits: 3, enemyTiles: enemies, allyTiles: allies).ToList();

        Assert.Equal(3, ranked.Count);
        // Cluster B center hits all 3 enemies in radius 1
        Assert.Equal((5, 5), ranked[0].Center);
        Assert.Equal(3, ranked[0].Enemies);
        // Cluster A hits 1
        Assert.Equal((10, 10), ranked[1].Center);
        // (2,2) hits none
        Assert.Equal((2, 2), ranked[2].Center);
        Assert.Equal(0, ranked[2].Enemies);
    }

    [Fact]
    public void RankCenters_EmptyCandidates_ReturnsEmpty()
    {
        var ranked = MultiHitTargetEnumerator.RankCenters(
            System.Array.Empty<(int x, int y)>(), aoeRadius: 1, hits: 3,
            enemyTiles: new HashSet<(int x, int y)>(), allyTiles: new HashSet<(int x, int y)>());
        Assert.Empty(ranked);
    }

    [Fact]
    public void Score_HitsField_ReflectsInputHitCount()
    {
        // Pass-through — caller uses this for info rendering.
        var enemies = new HashSet<(int x, int y)> { (5, 5) };
        var result = MultiHitTargetEnumerator.Score(
            candidateCenter: (5, 5), aoeRadius: 1, hits: 4,
            enemyTiles: enemies, allyTiles: new HashSet<(int x, int y)>());
        Assert.Equal(4, result.Hits);
    }
}
