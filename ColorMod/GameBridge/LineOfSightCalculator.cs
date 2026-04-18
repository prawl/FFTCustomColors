using System;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure function: decide whether a straight-line projectile from attacker
    /// tile to target tile clears all intermediate terrain. The caller provides
    /// a <c>heightAt(x, y)</c> callback so this module stays decoupled from
    /// MapData's concrete shape — tests inject fake terrain.
    ///
    /// This is "LoS Option B" per the TODO: a fallback to the memory-read
    /// damage preview which was ruled out in session 30
    /// (memory/project_damage_preview_hunt_s30.md).
    /// </summary>
    public static class LineOfSightCalculator
    {
        /// <summary>
        /// Walks the straight-line path from (ax,ay) to (tx,ty) via DDA (one
        /// step per integer cell in the longer axis). For each INTERMEDIATE
        /// tile, compares the tile's terrain height against the linearly-
        /// interpolated line-of-sight altitude at that tile. Returns false
        /// (blocked) if any intermediate tile's height exceeds the LoS
        /// altitude.
        ///
        /// Endpoints are excluded — a wall at the attacker's OR target's tile
        /// never blocks LoS to/from itself.
        /// </summary>
        /// <param name="ax">Attacker tile x.</param>
        /// <param name="ay">Attacker tile y.</param>
        /// <param name="attackerElevation">Attacker's effective launch height.</param>
        /// <param name="tx">Target tile x.</param>
        /// <param name="ty">Target tile y.</param>
        /// <param name="targetElevation">Target's effective hit height.</param>
        /// <param name="heightAt">Callback returning terrain blocking height at (x, y). Null → treat all tiles as unblocked.</param>
        public static bool HasLineOfSight(
            int ax, int ay, int attackerElevation,
            int tx, int ty, int targetElevation,
            Func<int, int, int>? heightAt)
        {
            if (heightAt == null) return true;
            if (ax == tx && ay == ty) return true;

            int dx = tx - ax;
            int dy = ty - ay;
            int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (steps <= 1) return true; // no intermediate tiles

            for (int i = 1; i < steps; i++)
            {
                // Linear-interpolated tile position. Round to nearest integer
                // cell — "tile the line crosses at this step".
                double t = (double)i / steps;
                int cx = (int)Math.Round(ax + dx * t);
                int cy = (int)Math.Round(ay + dy * t);

                // Skip cells that happen to land on endpoint coords (rounding
                // edge case).
                if ((cx == ax && cy == ay) || (cx == tx && cy == ty)) continue;

                double losAltitude = attackerElevation + (targetElevation - attackerElevation) * t;
                int terrain = heightAt(cx, cy);
                if (terrain > losAltitude) return false;
            }
            return true;
        }
    }
}
