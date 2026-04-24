using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// One-line result for "which direction should the unit face?" — combines
    /// the dx/dy vector, cardinal name, and the arc counts that rationalize
    /// the choice. <see cref="FromOverride"/> tells the caller whether a
    /// user-supplied facing was honored.
    /// </summary>
    public class FacingDecision
    {
        public int Dx { get; set; }
        public int Dy { get; set; }
        public string DirectionName { get; set; } = "";
        public int Front { get; set; }
        public int Side { get; set; }
        public int Back { get; set; }
        public bool FromOverride { get; set; }
    }

    /// <summary>
    /// Pure helper that unifies explicit-facing-override with auto-pick.
    /// When the caller (battle_wait shell command or scan_move's
    /// RecommendedFacing path) has a user-supplied direction, it wins;
    /// otherwise <see cref="FacingStrategy.ComputeOptimalFacingDetailed"/>
    /// picks the best arc against the living enemies.
    ///
    /// Session 47: replaces the scattered `facingOverride ?? FacingStrategy...`
    /// pattern with one call. Ships with 10 tests pinning both branches +
    /// the cardinal-name formatting.
    /// </summary>
    public static class FacingDecider
    {
        public static FacingDecision Decide(
            (int dx, int dy)? facingOverride,
            FacingStrategy.UnitPosition ally,
            List<FacingStrategy.UnitPosition> enemies)
        {
            int dx, dy;
            int front = 0, side = 0, back = 0;
            bool fromOverride;

            if (facingOverride.HasValue)
            {
                (dx, dy) = facingOverride.Value;
                fromOverride = true;
                // For override paths the arc counts aren't computed — caller
                // asked for a specific facing; that trumps the counts. If a
                // future use case needs them, call ComputeOptimalFacingDetailed
                // separately and pass through.
            }
            else
            {
                var result = FacingStrategy.ComputeOptimalFacingDetailed(ally, enemies);
                dx = result.Dx;
                dy = result.Dy;
                front = result.Front;
                side = result.Side;
                back = result.Back;
                fromOverride = false;
            }

            return new FacingDecision
            {
                Dx = dx,
                Dy = dy,
                DirectionName = NameFor(dx, dy),
                Front = front,
                Side = side,
                Back = back,
                FromOverride = fromOverride,
            };
        }

        /// <summary>
        /// Cardinal name for a (dx,dy) pair. Non-cardinal pairs fall back
        /// to "(dx,dy)" formatting.
        ///
        /// Grid convention: +y increases going SOUTH (matches FFT's in-memory
        /// coordinate system and <see cref="FacingByteDecoder"/>). Previous
        /// versions of this helper used the reverse labeling, producing
        /// "Face North" recommendations that pointed the unit south in-game
        /// — live-observed 2026-04-24 Lenalian Plateau.
        /// </summary>
        public static string NameFor(int dx, int dy) => (dx, dy) switch
        {
            (1, 0) => "East",
            (-1, 0) => "West",
            (0, 1) => "South",  // +y = south in FFT grid
            (0, -1) => "North", // -y = north in FFT grid
            _ => $"({dx},{dy})",
        };
    }
}
