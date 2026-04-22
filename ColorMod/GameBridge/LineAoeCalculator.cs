using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    public enum LineDirection { North, South, East, West }

    /// <summary>
    /// Pure helper for Line AoE ability target enumeration (Shockwave,
    /// Ice Saber, etc.). Produces the ordered list of tiles a cardinal-
    /// direction line hits from a caster, and picks the best direction
    /// for maximum enemy coverage.
    ///
    /// Coordinate convention: +X east, +Y south (screen coords).
    /// Caster's own tile is NOT included — the line starts one tile out.
    /// </summary>
    public static class LineAoeCalculator
    {
        public static IEnumerable<(int x, int y)> Enumerate(
            int casterX, int casterY, LineDirection direction, int range)
        {
            if (range <= 0) yield break;
            int dx = 0, dy = 0;
            switch (direction)
            {
                case LineDirection.North: dy = -1; break;
                case LineDirection.South: dy = 1; break;
                case LineDirection.East:  dx = 1; break;
                case LineDirection.West:  dx = -1; break;
            }
            for (int i = 1; i <= range; i++)
                yield return (casterX + dx * i, casterY + dy * i);
        }

        /// <summary>
        /// Pick the cardinal direction that hits the most enemy tiles from
        /// the given caster position and range. Ties broken by
        /// North &lt; South &lt; East &lt; West. Returns null if no direction hits
        /// any enemy.
        /// </summary>
        public static LineDirection? PickBestDirection(
            int casterX, int casterY, int range,
            HashSet<(int x, int y)> enemyTiles)
        {
            LineDirection? best = null;
            int bestCount = 0;
            foreach (LineDirection dir in new[] {
                LineDirection.North, LineDirection.South,
                LineDirection.East, LineDirection.West })
            {
                int count = 0;
                foreach (var t in Enumerate(casterX, casterY, dir, range))
                    if (enemyTiles.Contains(t)) count++;
                if (count > bestCount)
                {
                    best = dir;
                    bestCount = count;
                }
            }
            return best;
        }
    }
}
