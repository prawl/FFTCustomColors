using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Helper methods for battlefield spatial queries.
    /// </summary>
    public static class BattleFieldHelper
    {
        /// <summary>
        /// Returns positions of all units EXCEPT the active unit.
        /// Includes allies, enemies, dead units, guests — any tile that is occupied.
        /// The active unit is excluded because the BFS starts from their position.
        /// Units with invalid positions (x &lt; 0) are excluded.
        /// </summary>
        public static HashSet<(int, int)> GetOccupiedPositions(
            List<(int x, int y, int team, int hp, bool isActive)> units)
        {
            var result = new HashSet<(int, int)>();
            foreach (var u in units)
            {
                if (u.isActive) continue;
                if (u.x < 0 || u.y < 0) continue;
                result.Add((u.x, u.y));
            }
            return result;
        }
    }
}
