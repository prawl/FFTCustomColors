using System;
using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure BFS flood-fill from a start tile, expanding through cardinal
    /// neighbors as long as the supplied predicate reports the neighbor
    /// as valid. Used by the BFS-verify diagnostic to discover the game's
    /// actual valid-move tiles by probing the cursor flag at each tile.
    /// </summary>
    public static class CursorFloodFill
    {
        public static HashSet<(int x, int y)> Flood(
            int startX, int startY, Func<(int x, int y), bool> isValid)
        {
            var result = new HashSet<(int, int)>();
            var visited = new HashSet<(int, int)>();

            var start = (startX, startY);
            if (!isValid(start))
            {
                visited.Add(start);
                return result;
            }

            result.Add(start);
            visited.Add(start);
            var queue = new Queue<(int x, int y)>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var neighbor = (cx + dx, cy + dy);
                    if (!visited.Add(neighbor)) continue;
                    if (isValid(neighbor))
                    {
                        result.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return result;
        }
    }
}
