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

        /// <summary>
        /// Classify the occupant label for the Attack-tiles render:
        ///   - "empty" when no unit occupies the tile
        ///   - "ally" / "enemy" when the occupant is attackable
        ///   - "dead" / "crystal" / "treasure" / "petrified" otherwise
        ///
        /// Returning a specific corpse label (instead of collapsing to
        /// "empty") lets Claude see that an adjacent corpse tile isn't a
        /// valid attack target while still making the tile's real state
        /// visible for planning (e.g. move-through pathing, crystal
        /// pickup). Live-flagged 2026-04-24.
        /// </summary>
        public static string ClassifyOccupant(
            bool hasOccupant, int hp, byte[]? statusBytes, int team)
        {
            if (!hasOccupant) return "empty";
            if (IsAttackable(hp, statusBytes))
                return team == 0 ? "ally" : "enemy";
            // Unattackable occupant — surface life state. When HP<=0 but
            // the dead status bit hasn't propagated yet, GetLifeState
            // returns "alive"; fall back to "dead" for clarity.
            var lifeState = statusBytes != null
                ? StatusDecoder.GetLifeState(statusBytes)
                : "alive";
            return lifeState == "alive" ? "dead" : lifeState;
        }
    }
}
