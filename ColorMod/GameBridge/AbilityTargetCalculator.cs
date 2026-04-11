using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Computes the set of tiles a single-tile-target ability can be aimed at.
    ///
    /// Scope (V1): only point-target abilities — AoE=1, HRange is a numeric range,
    /// the ability hits exactly the tile the player clicks. Excludes self-only
    /// abilities, radius splashes, cardinal lines, and full-field effects.
    ///
    /// The valid target set is every in-bounds tile whose taxicab distance from the
    /// caster is within HR and whose elevation delta is within VR. Line of sight,
    /// friend/foe filtering, and walkability are NOT applied here — callers can
    /// filter further if they want. FFT lets you target empty/unwalkable tiles with
    /// many abilities, so we return them in the raw set.
    /// </summary>
    public static class AbilityTargetCalculator
    {
        /// <summary>
        /// Returns true if this ability is a single-tile point-target ability eligible
        /// for the V1 calculator. Filters out self-targeted, AoE splash, and
        /// non-numeric HRange values.
        /// </summary>
        public static bool IsPointTarget(ActionAbilityInfo ability)
        {
            if (ability.AoE != 1) return false;
            if (ability.HRange == "Self") return false;
            if (!int.TryParse(ability.HRange, out _)) return false;
            return true;
        }

        /// <summary>
        /// Compute valid target tiles for a point-target ability cast from the given
        /// caster position on the given map. Returns an empty set if the ability
        /// isn't eligible or the map is null.
        /// </summary>
        public static HashSet<(int x, int y)> GetValidTargetTiles(
            int casterX,
            int casterY,
            ActionAbilityInfo ability,
            MapData? map)
        {
            var result = new HashSet<(int, int)>();
            if (map == null) return result;
            if (!IsPointTarget(ability)) return result;
            if (!int.TryParse(ability.HRange, out int hr)) return result;

            int casterZ = map.InBounds(casterX, casterY)
                ? map.Tiles[casterX, casterY].Height
                : 0;

            int vr = ability.VRange; // 99 acts as "unlimited" naturally

            // Enemy-target abilities can't be aimed at the caster's own tile.
            // Ally-target abilities CAN — you can heal yourself with Potion, Chant, Salve, etc.
            // Target strings: "enemy", "ally", "self", "ally/AoE", "enemy/AoE", "AoE".
            bool includeSelfTile = ability.Target.Contains("ally") || ability.Target.Contains("self");

            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    if (x == casterX && y == casterY && !includeSelfTile) continue;
                    // Skip tiles that don't exist on the map (holes, void).
                    if (!map.IsWalkable(x, y)) continue;
                    int taxi = System.Math.Abs(x - casterX) + System.Math.Abs(y - casterY);
                    if (taxi > hr) continue;
                    int zDelta = System.Math.Abs(map.Tiles[x, y].Height - casterZ);
                    if (zDelta > vr) continue;
                    result.Add((x, y));
                }
            }

            return result;
        }
    }
}
