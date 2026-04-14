using System;
using System.Collections.Generic;
using System.Linq;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// BFS computation of valid movement tiles using JSON map data.
    /// Extracted from CommandWatcher for testability.
    /// </summary>
    public static class MovementBfs
    {
        /// <summary>
        /// Apply movement ability bonuses to base Move/Jump stats.
        /// Movement +1/+2/+3 increase Move. Jump +1/+2/+3 increase Jump.
        /// Other movement abilities (Teleport, Fly, Manafont, etc.) don't change stats.
        /// </summary>
        public static (int move, int jump) ApplyMovementAbility(int baseMove, int baseJump, string? movementAbilityName)
        {
            if (string.IsNullOrEmpty(movementAbilityName))
                return (baseMove, baseJump);

            if (movementAbilityName.StartsWith("Movement +") && int.TryParse(movementAbilityName.Substring(10), out int moveBonus))
                return (baseMove + moveBonus, baseJump);

            if (movementAbilityName.StartsWith("Jump +") && int.TryParse(movementAbilityName.Substring(6), out int jumpBonus))
                return (baseMove, baseJump + jumpBonus);

            return (baseMove, baseJump);
        }

        /// <summary>
        /// Compute the set of tiles a unit can move to from (unitX, unitY).
        /// Uses map terrain data for height checks and movement costs.
        /// Enemy-occupied tiles block both traversal and destination.
        /// Ally-occupied tiles are handled by the allyPositions parameter.
        /// The unit's own tile is excluded from the result.
        /// </summary>
        public static List<TilePosition> ComputeValidTiles(
            MapData map, int unitX, int unitY, int moveStat, int jumpStat,
            HashSet<(int, int)>? enemyPositions = null,
            HashSet<(int, int)>? allyPositions = null)
        {
            double GetDisplayHeight(int x, int y)
            {
                if (!map.InBounds(x, y)) return -1;
                var t = map.Tiles[x, y];
                return t.Height + t.SlopeHeight / 2.0;
            }

            var visited = new Dictionary<(int, int), int>();
            var queue = new Queue<(int x, int y, int cost)>();

            if (!map.InBounds(unitX, unitY)) return new List<TilePosition>();

            queue.Enqueue((unitX, unitY, 0));
            visited[(unitX, unitY)] = 0;

            int[][] dirs = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };

            while (queue.Count > 0)
            {
                var (x, y, cost) = queue.Dequeue();
                if (cost >= moveStat) continue;

                double ch = GetDisplayHeight(x, y);

                foreach (var d in dirs)
                {
                    int nx = x + d[0], ny = y + d[1];

                    if (!map.InBounds(nx, ny)) continue;
                    if (!map.IsWalkable(nx, ny)) continue;
                    if (enemyPositions != null && enemyPositions.Contains((nx, ny))) continue;

                    double nh = GetDisplayHeight(nx, ny);
                    if (nh < 0 || ch < 0) continue;

                    if (Math.Abs(nh - ch) > jumpStat) continue;

                    int tileCost = map.Tiles[nx, ny].MoveCost;
                    // Walking through an ally-occupied tile costs +1 move point
                    if (allyPositions != null && allyPositions.Contains((nx, ny)))
                        tileCost += 1;
                    int newCost = cost + tileCost;
                    if (newCost > moveStat) continue;
                    if (!visited.ContainsKey((nx, ny)) || visited[(nx, ny)] > newCost)
                    {
                        visited[(nx, ny)] = newCost;
                        queue.Enqueue((nx, ny, newCost));
                    }
                }
            }

            // Exclude the starting tile and ally-occupied tiles (can walk through but can't stop)
            visited.Remove((unitX, unitY));
            if (allyPositions != null)
                foreach (var pos in allyPositions)
                    visited.Remove(pos);

            return visited
                .OrderBy(kv => kv.Value)
                .ThenBy(kv => Math.Abs(kv.Key.Item1 - unitX) + Math.Abs(kv.Key.Item2 - unitY))
                .Select(kv => new TilePosition { X = kv.Key.Item1, Y = kv.Key.Item2 })
                .ToList();
        }
    }
}
