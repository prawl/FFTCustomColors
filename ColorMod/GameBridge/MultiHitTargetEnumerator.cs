using System.Collections.Generic;
using System.Linq;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Score + ranking of AoE center tiles for multi-hit abilities
    /// (Ramuh, Bio, Shiva, etc.). Doesn't pick actual random strikes —
    /// returns enemy/ally coverage per candidate so scan_move can surface
    /// the best center tile for the caller to target.
    /// </summary>
    public static class MultiHitTargetEnumerator
    {
        public record CenterScore(
            (int x, int y) Center,
            int Enemies,
            int Allies,
            int Hits)
        {
            public int Net => Enemies - Allies;
        }

        public static CenterScore Score(
            (int x, int y) candidateCenter,
            int aoeRadius,
            int hits,
            HashSet<(int x, int y)> enemyTiles,
            HashSet<(int x, int y)> allyTiles)
        {
            int enemies = 0;
            int allies = 0;
            foreach (var t in SelfCenteredAoeCalculator.Enumerate(
                candidateCenter.x, candidateCenter.y, aoeRadius, includeSelf: true))
            {
                if (enemyTiles.Contains(t)) enemies++;
                if (allyTiles.Contains(t)) allies++;
            }
            return new CenterScore(candidateCenter, enemies, allies, hits);
        }

        public static IEnumerable<CenterScore> RankCenters(
            IEnumerable<(int x, int y)> candidates,
            int aoeRadius,
            int hits,
            HashSet<(int x, int y)> enemyTiles,
            HashSet<(int x, int y)> allyTiles)
        {
            return candidates
                .Select(c => Score(c, aoeRadius, hits, enemyTiles, allyTiles))
                .OrderByDescending(s => s.Net)
                .ThenByDescending(s => s.Enemies);
        }
    }
}
