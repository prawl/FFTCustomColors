using System.Collections.Generic;
using System.Linq;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Sort move-tile lists by min Manhattan distance to nearest enemy so
    /// the most-actionable repositions appear first. The original BFS-
    /// visit order is unhelpful for an LLM driver — they have to mentally
    /// cross-ref against the unit list to find tiles near enemies.
    /// Live-flagged 2026-04-25 playtest. Pure helper.
    /// </summary>
    public static class MoveTileSorter
    {
        public static List<TilePosition> SortByNearestEnemy(
            IReadOnlyList<TilePosition> tiles,
            IReadOnlyList<(int x, int y)> enemyPositions)
        {
            if (tiles == null || tiles.Count == 0) return new List<TilePosition>();
            // No enemies = no signal to sort by. Return original order.
            if (enemyPositions == null || enemyPositions.Count == 0)
                return tiles.ToList();

            return tiles
                .Select((t, idx) => (tile: t, idx, dist: MinDistance(t.X, t.Y, enemyPositions)))
                .OrderBy(x => x.dist)
                .ThenBy(x => x.idx)            // stable: tied distances keep input order
                .Select(x => x.tile)
                .ToList();
        }

        private static int MinDistance(int x, int y, IReadOnlyList<(int x, int y)> enemies)
        {
            int min = int.MaxValue;
            foreach (var e in enemies)
            {
                int d = System.Math.Abs(x - e.x) + System.Math.Abs(y - e.y);
                if (d < min) min = d;
            }
            return min;
        }
    }
}
