using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Computes the set of tiles a single-tile-target or radius-AoE ability can
    /// be aimed at, and the splash set for radius abilities.
    ///
    /// Scope:
    ///   - Point-target abilities: AoE=1, numeric HRange. Hits exactly the clicked tile.
    ///   - Radius AoE abilities: AoE>1, numeric HRange. Diamond splash of radius
    ///     (AoE-1) around the clicked tile, filtered by HoE elevation delta.
    ///
    /// Excludes self-only (HRange="Self"), line shapes, cone shapes, and
    /// full-field effects — those need different calculators.
    ///
    /// Line of sight is NOT modeled. FFT lets you target empty or unwalkable tiles
    /// with many abilities, so we return all geometrically-valid tiles; callers
    /// can filter further if needed.
    /// </summary>
    public static class AbilityTargetCalculator
    {
        /// <summary>
        /// Returns true if this ability is a single-tile point-target ability
        /// (AoE=1, numeric HRange, not self-cast).
        /// </summary>
        public static bool IsPointTarget(ActionAbilityInfo ability)
        {
            if (ability.AoE != 1) return false;
            if (ability.HRange == "Self") return false;
            if (!int.TryParse(ability.HRange, out _)) return false;
            return true;
        }

        /// <summary>
        /// Returns true if this ability is a radius-AoE ability with a clickable
        /// target tile (AoE>1, numeric HRange, not self-cast). Fira, Cure,
        /// Protect, Summons, Ultima, etc.
        /// </summary>
        public static bool IsRadiusTarget(ActionAbilityInfo ability)
        {
            if (ability.AoE <= 1) return false;
            if (ability.HRange == "Self") return false;
            if (!int.TryParse(ability.HRange, out _)) return false;
            return true;
        }

        /// <summary>
        /// Compute the set of valid target-center tiles for a point-target OR
        /// radius-AoE ability cast from the given caster position. Same math for
        /// both shapes — taxicab ≤ HR, ΔZ ≤ VR, map bounds, walkable.
        /// Returns an empty set if the ability isn't eligible or the map is null.
        /// </summary>
        public static HashSet<(int x, int y)> GetValidTargetTiles(
            int casterX,
            int casterY,
            ActionAbilityInfo ability,
            MapData? map)
        {
            var result = new HashSet<(int, int)>();
            if (map == null) return result;
            if (!IsPointTarget(ability) && !IsRadiusTarget(ability)) return result;
            if (!int.TryParse(ability.HRange, out int hr)) return result;

            int casterZ = map.InBounds(casterX, casterY)
                ? map.Tiles[casterX, casterY].Height
                : 0;

            int vr = ability.VRange; // 99 acts as "unlimited" naturally

            // Enemy-target abilities can't be aimed at the caster's own tile for
            // point-target casts. Radius casts CAN land their center on the caster
            // (the splash still catches anyone standing nearby), but the game may
            // still disallow it — we allow it for now and let empirical testing
            // refine this.
            bool includeSelfTile =
                ability.Target.Contains("ally") ||
                ability.Target.Contains("self") ||
                IsRadiusTarget(ability);

            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    if (x == casterX && y == casterY && !includeSelfTile) continue;
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

        /// <summary>
        /// Compute the splash tile set for a radius-AoE ability centered on the
        /// given target tile. Diamond of radius (AoE-1) around (centerX, centerY),
        /// filtered by HoE elevation delta from the center tile and walkability.
        ///
        /// Returns an empty set for ineligible abilities (point-target or
        /// non-radius). Point-target abilities should use GetValidTargetTiles
        /// directly — their "splash" is just the clicked tile.
        /// </summary>
        public static HashSet<(int x, int y)> GetSplashTiles(
            int centerX,
            int centerY,
            ActionAbilityInfo ability,
            MapData? map)
        {
            var result = new HashSet<(int, int)>();
            if (map == null) return result;
            if (!IsRadiusTarget(ability)) return result;

            int radius = ability.AoE - 1;
            int hoe = ability.HoE; // elevation tolerance from the center tile

            if (!map.InBounds(centerX, centerY)) return result;
            int centerZ = map.Tiles[centerX, centerY].Height;

            for (int dx = -radius; dx <= radius; dx++)
            {
                int rowBudget = radius - System.Math.Abs(dx);
                for (int dy = -rowBudget; dy <= rowBudget; dy++)
                {
                    int x = centerX + dx;
                    int y = centerY + dy;
                    if (!map.InBounds(x, y)) continue;
                    if (!map.IsWalkable(x, y)) continue;
                    int zDelta = System.Math.Abs(map.Tiles[x, y].Height - centerZ);
                    if (zDelta > hoe) continue;
                    result.Add((x, y));
                }
            }

            return result;
        }
    }
}
