using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure helper: enumerate tiles within a Manhattan-distance radius
    /// of a caster (the classic FFT diamond AoE shape). Used by self-
    /// centered AoE abilities like Chakra, Cyclone, Wave Fist, Bard
    /// and Dancer songs.
    ///
    /// Caller controls whether the caster's own tile is included — e.g.
    /// Chakra heals the caster (includeSelf=true), Cyclone hits only
    /// adjacent tiles (includeSelf=false).
    /// </summary>
    public static class SelfCenteredAoeCalculator
    {
        public static IEnumerable<(int x, int y)> Enumerate(
            int casterX, int casterY, int radius, bool includeSelf)
        {
            if (radius < 0) yield break;
            for (int dy = -radius; dy <= radius; dy++)
            {
                int remaining = radius - System.Math.Abs(dy);
                for (int dx = -remaining; dx <= remaining; dx++)
                {
                    if (!includeSelf && dx == 0 && dy == 0) continue;
                    yield return (casterX + dx, casterY + dy);
                }
            }
        }

        /// <summary>
        /// Count enemy tiles hit by the self-centered AoE. Convenience for
        /// decision scoring.
        /// </summary>
        public static int CountEnemiesHit(
            int casterX, int casterY, int radius, bool includeSelf,
            HashSet<(int x, int y)> enemyTiles)
        {
            int count = 0;
            foreach (var t in Enumerate(casterX, casterY, radius, includeSelf))
                if (enemyTiles.Contains(t)) count++;
            return count;
        }
    }
}
