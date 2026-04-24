namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Decides whether an occupant of an adjacent cardinal tile should
    /// be rendered as an attack target in scan_move output. An occupant
    /// is NOT attackable if:
    ///   - statusBytes decode as dead / crystal / treasure / petrified
    ///     via <see cref="StatusDecoder.GetLifeState"/>, OR
    ///   - HP &lt;= 0 (animation-transient defense — a unit reading HP=0
    ///     with status bytes not yet propagated to the dead flag still
    ///     shouldn't be a valid attack target).
    ///
    /// Extracted from NavigationActions inline logic 2026-04-24 after a
    /// TODO entry flagged "Attack tiles lists dead enemies as valid
    /// targets without marker". The existing GetLifeState check already
    /// filtered status-flagged corpses; the HP&gt;0 guard closes the
    /// remaining edge case.
    /// </summary>
    public static class AttackTileOccupantClassifier
    {
        public static bool IsAttackable(int hp, byte[]? statusBytes)
        {
            if (hp <= 0) return false;
            if (statusBytes != null && StatusDecoder.GetLifeState(statusBytes) != "alive")
                return false;
            return true;
        }
    }
}
