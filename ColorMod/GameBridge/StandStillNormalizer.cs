namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Normalize an <c>execute_turn</c>'s move target when it matches the
    /// caster's current tile. The BFS-emitted move list excludes the
    /// origin tile by design, so a "stand still + act" call like
    /// <c>execute_turn 7 9 "Phoenix Down" 6 9</c> while standing on (7,9)
    /// would otherwise fail validation with "Tile (7,9) is not in the
    /// valid move range." Treating same-tile as "no move" lets the bundle
    /// skip battle_move and dispatch just the ability.
    /// Live-flagged 2026-04-25 playtest.
    /// </summary>
    public static class StandStillNormalizer
    {
        public static (int? moveX, int? moveY) NormalizeSameTile(
            int? moveX, int? moveY, int? currentX, int? currentY)
        {
            if (moveX == null || moveY == null) return (moveX, moveY);
            if (currentX == null || currentY == null) return (moveX, moveY);
            if (moveX.Value == currentX.Value && moveY.Value == currentY.Value)
                return (null, null);
            return (moveX, moveY);
        }
    }
}
