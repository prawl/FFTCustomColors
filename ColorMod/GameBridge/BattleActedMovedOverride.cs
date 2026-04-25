namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Override the raw battleActed (0x14077CA8C) and battleMoved
    /// (0x14077CA9C) bytes when the bridge has tracked the action
    /// commit via its private flags. The raw bytes read 0 transiently
    /// right after a confirmed action — Phoenix Down and Throw Stone
    /// both live-observed with battleActed: 0 in the response despite
    /// the action clearly resolving. Without this override, callers
    /// see an inconsistent acted/moved signal between the UI tag
    /// (computed from the flags) and the raw response fields.
    ///
    /// The flags (`_actedThisTurn` / `_movedThisTurn` in CommandWatcher)
    /// reset on turn boundaries, so the override is naturally bounded
    /// to the current turn.
    /// </summary>
    public static class BattleActedMovedOverride
    {
        public static (int acted, int moved) Apply(
            int rawActed, int rawMoved, bool actedFlag, bool movedFlag)
        {
            return (
                actedFlag ? 1 : rawActed,
                movedFlag ? 1 : rawMoved);
        }
    }
}
