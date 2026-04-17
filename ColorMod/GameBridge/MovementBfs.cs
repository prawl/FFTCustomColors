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
            var visited = new Dictionary<(int, int), int>();
            var queue = new Queue<(int x, int y, int cost)>();

            if (!map.InBounds(unitX, unitY)) return new List<TilePosition>();

            queue.Enqueue((unitX, unitY, 0));
            visited[(unitX, unitY)] = 0;

            // Dir vectors paired with the Direction enum for edge-height lookup.
            // dx/dy are added to (x,y) to get the neighbour; exitDir is the edge
            // of the CURRENT tile we're leaving toward; entryDir is the edge of
            // the NEXT tile we're entering through (the opposite side).
            var steps = new (int dx, int dy, Direction exit, Direction entry)[]
            {
                (1, 0, Direction.E, Direction.W),
                (-1, 0, Direction.W, Direction.E),
                (0, 1, Direction.S, Direction.N),
                (0, -1, Direction.N, Direction.S),
            };

            while (queue.Count > 0)
            {
                var (x, y, cost) = queue.Dequeue();
                if (cost >= moveStat) continue;

                foreach (var s in steps)
                {
                    int nx = x + s.dx, ny = y + s.dy;

                    if (!map.InBounds(nx, ny)) continue;
                    if (!map.IsWalkable(nx, ny)) continue;
                    if (enemyPositions != null && enemyPositions.Contains((nx, ny))) continue;

                    // Canonical FFT height check: compare the CURRENT tile's
                    // exit-edge height against the NEXT tile's entry-edge
                    // height. Slopes raise the high edge by slope_height,
                    // so a unit leaving a Flat 5 tile northward onto an
                    // Incline-N tile enters at the incline's N-edge (= h + sh),
                    // not the average display height. This matters for jump
                    // checks on terrain with slopes and convex corners.
                    double exitH = TileEdgeHeight.Edge(map.Tiles[x, y], s.exit);
                    double entryH = TileEdgeHeight.Edge(map.Tiles[nx, ny], s.entry);

                    if (Math.Abs(exitH - entryH) > jumpStat) continue;

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
