using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Computes the set of tiles a single-tile-target, radius-AoE, or cardinal
    /// line ability can be aimed at, and the splash/hit set for each.
    ///
    /// Scope:
    ///   - Point-target (AoE=1, numeric HRange): clicks hit exactly the clicked tile.
    ///   - Radius AoE (AoE>1, numeric HRange): diamond splash of radius (AoE-1)
    ///     around the clicked tile, filtered by HoE elevation delta.
    ///   - Line (Shape=Line, numeric HRange): caster picks a cardinal direction
    ///     by clicking a seed tile (N/E/S/W neighbor); the line extends HR tiles
    ///     in that direction. HoE hard-terminates the line at the first tile whose
    ///     |ΔZ| from the caster exceeds the tolerance.
    ///
    /// Excludes self-only (HRange="Self"), cone shapes, and full-field effects —
    /// those need different calculators.
    ///
    /// Line of sight is NOT modeled. FFT lets you target empty or unwalkable tiles
    /// with many abilities, so we return all geometrically-valid tiles; callers
    /// can filter further if needed.
    /// </summary>
    public static class AbilityTargetCalculator
    {
        /// <summary>
        /// The four cardinal direction vectors used by Line-shape abilities.
        /// Labeled with compass strings for display and scripting.
        /// </summary>
        public static readonly (string Label, int Dx, int Dy)[] CardinalDirections =
        {
            ("N", 0, -1),
            ("E", 1, 0),
            ("S", 0, 1),
            ("W", -1, 0),
        };

        /// <summary>
        /// Returns true if this ability is a single-tile point-target ability
        /// (AoE=1, numeric HRange, not self-cast, not an explicitly-tagged shape).
        /// </summary>
        public static bool IsPointTarget(ActionAbilityInfo ability)
        {
            if (ability.Shape != AbilityShape.Auto) return false;
            if (ability.AoE != 1) return false;
            if (ability.HRange == "Self") return false;
            if (!int.TryParse(ability.HRange, out _)) return false;
            return true;
        }

        /// <summary>
        /// Returns true if this ability is a radius-AoE ability with a clickable
        /// target tile (AoE>1, numeric HRange, not self-cast, not an explicitly-tagged shape).
        /// Fira, Cure, Protect, Summons, Ultima, etc.
        /// </summary>
        public static bool IsRadiusTarget(ActionAbilityInfo ability)
        {
            if (ability.Shape != AbilityShape.Auto) return false;
            if (ability.AoE <= 1) return false;
            if (ability.HRange == "Self") return false;
            if (!int.TryParse(ability.HRange, out _)) return false;
            return true;
        }

        /// <summary>
        /// Returns true if this ability fires a cardinal line from the caster
        /// (explicitly tagged with Shape=Line). Shockwave, Divine Ruination.
        /// </summary>
        public static bool IsLineTarget(ActionAbilityInfo ability)
        {
            if (ability.Shape != AbilityShape.Line) return false;
            if (ability.HRange == "Self") return false;
            if (!int.TryParse(ability.HRange, out _)) return false;
            return true;
        }

        /// <summary>
        /// Compute the set of valid target-tiles the caster can aim at. For
        /// point-target and radius-AoE abilities, this is the taxicab diamond
        /// within HR/VR (the clicked tile is either the hit or the splash center).
        /// For line abilities, this is the 4 cardinal seed tiles that pick a
        /// direction. Returns an empty set if the ability isn't eligible or the
        /// map is null.
        ///
        /// casterJump: optional fallback when the ability's VRange is 0. In FFT,
        /// melee-range abilities don't define their own vertical tolerance —
        /// they inherit the caster's Jump stat. Passing it here avoids culling
        /// valid melee targets on sloped maps. Callers can pass 0 to disable.
        /// </summary>
        public static HashSet<(int x, int y)> GetValidTargetTiles(
            int casterX,
            int casterY,
            ActionAbilityInfo ability,
            MapData? map,
            int casterJump = 0)
        {
            var result = new HashSet<(int, int)>();
            if (map == null) return result;

            // Line abilities: 4 cardinal seed tiles around the caster.
            // Each seed tile picks a direction for the line to fire in.
            if (IsLineTarget(ability))
            {
                int casterZSeed = map.InBounds(casterX, casterY)
                    ? map.Tiles[casterX, casterY].Height
                    : 0;
                foreach (var (_, dx, dy) in CardinalDirections)
                {
                    int sx = casterX + dx;
                    int sy = casterY + dy;
                    if (!map.InBounds(sx, sy)) continue;
                    if (!map.IsWalkable(sx, sy)) continue;
                    // Seed tile must be within the line's HoE tolerance from the
                    // caster — otherwise the game won't let you aim that direction.
                    int zDelta = System.Math.Abs(map.Tiles[sx, sy].Height - casterZSeed);
                    if (zDelta > ability.HoE) continue;
                    result.Add((sx, sy));
                }
                return result;
            }

            if (!IsPointTarget(ability) && !IsRadiusTarget(ability)) return result;
            if (!int.TryParse(ability.HRange, out int hr)) return result;

            int casterZ = map.InBounds(casterX, casterY)
                ? map.Tiles[casterX, casterY].Height
                : 0;

            // VR=0 in the table means "melee, use caster Jump as vertical reach".
            // Real vertical-range abilities (Throw Stone, Black Magicks) have VR=99.
            int vr = ability.VRange > 0 ? ability.VRange : casterJump;

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
        /// Compute the set of tiles hit by a line-shape ability fired from the
        /// caster in the given cardinal direction. Walks outward HR tiles. The
        /// caster's own tile is not included (the line starts one tile past the
        /// caster). HoE is a hard terminator — the first tile whose |ΔZ| from
        /// the caster exceeds the tolerance stops the line. Map edge also stops
        /// the line. Returns an empty set for non-line abilities or null map.
        /// </summary>
        public static List<(int x, int y)> GetLineTiles(
            int casterX,
            int casterY,
            int dx,
            int dy,
            ActionAbilityInfo ability,
            MapData? map)
        {
            var result = new List<(int, int)>();
            if (map == null) return result;
            if (!IsLineTarget(ability)) return result;
            if (!int.TryParse(ability.HRange, out int length)) return result;
            if (!map.InBounds(casterX, casterY)) return result;

            int casterZ = map.Tiles[casterX, casterY].Height;
            int hoe = ability.HoE;

            for (int step = 1; step <= length; step++)
            {
                int x = casterX + dx * step;
                int y = casterY + dy * step;
                if (!map.InBounds(x, y)) break;
                int zDelta = System.Math.Abs(map.Tiles[x, y].Height - casterZ);
                if (zDelta > hoe) break; // hard terminator: the line stops here
                result.Add((x, y));
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
