using System;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure function: compute the attack arc (front/side/back) from an attacker's
    /// tile to a target's tile given the target's cardinal facing name. Returns
    /// a one-word marker or null on degenerate inputs (same tile, unknown facing).
    ///
    /// Screen-coord convention (y grows south):
    ///   North = (0, -1)   East = (+1, 0)
    ///   South = (0, +1)   West = (-1, 0)
    ///
    /// Arc rule: project the attacker-relative vector onto the target's facing.
    ///   dot &gt; 0 → front (attacker is on the side the target faces)
    ///   dot &lt; 0 → back  (attacker is behind)
    ///   dot = 0 → side  (perpendicular, tie)
    /// </summary>
    public static class BackstabArcCalculator
    {
        public static string? ComputeArc(int attackerX, int attackerY, int targetX, int targetY, string? targetFacing)
        {
            int relX = attackerX - targetX;
            int relY = attackerY - targetY;
            if (relX == 0 && relY == 0) return null;

            int fdx, fdy;
            switch ((targetFacing ?? "").Trim().ToLowerInvariant())
            {
                case "north": fdx = 0; fdy = -1; break;
                case "south": fdx = 0; fdy = +1; break;
                case "east":  fdx = +1; fdy = 0; break;
                case "west":  fdx = -1; fdy = 0; break;
                default: return null;
            }

            int dot = relX * fdx + relY * fdy;
            if (dot > 0) return "front";
            if (dot < 0) return "back";
            return "side";
        }

        /// <summary>
        /// Relative desirability score for attack arc selection. Back is most
        /// desirable (highest hit% + crit bonus in canonical FFT), side is
        /// mildly preferred over front. These are heuristic weights for target
        /// ranking, not literal hit-% modifiers.
        /// </summary>
        public static int ArcBonusScore(string? arc)
        {
            return arc switch
            {
                "back" => 10,
                "side" => 3,
                "front" => 0,
                _ => 0,
            };
        }
    }
}
