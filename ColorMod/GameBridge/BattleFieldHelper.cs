using System.Collections.Generic;
using System.Linq;

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

        /// <summary>
        /// Returns true if all enemy units (team 1) are defeated.
        /// Defeated means: dead, crystal, treasure, or petrified.
        /// Returns false if there are no enemies (edge case / data issue).
        /// Note: story battles may have special win conditions (e.g. kill boss only) —
        /// this check covers random encounters and standard battles.
        /// </summary>
        public static bool AllEnemiesDefeated(List<BattleUnitState> units)
        {
            var enemies = units.Where(u => u.Team == 1).ToList();
            if (enemies.Count == 0) return false;
            return enemies.All(e => IsDefeated(e));
        }

        private static bool IsDefeated(BattleUnitState unit)
        {
            if (unit.Hp <= 0) return true;
            if (unit.LifeState is "dead" or "crystal" or "treasure") return true;
            if (unit.Statuses?.Contains("Petrify") == true) return true;
            return false;
        }
    }
}
