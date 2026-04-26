namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Builds a loud `=== TURN HANDOFF: A → B (x,y) HP=h/mh ===` banner
    /// when the active player unit changes between the start and end of
    /// a bundled turn (`execute_turn` / `battle_wait`). Multi-unit party
    /// agents otherwise lose track of which unit is now active and issue
    /// commands meant for the prior unit (live-flagged 2026-04-25).
    /// Pure helper — caller snapshots identity before clearing the
    /// active-unit cache, then re-reads after auto-scan repopulates it.
    /// </summary>
    public static class TurnHandoffBannerClassifier
    {
        public record UnitIdentity(string? Name, string? JobName, int X, int Y, int Hp, int MaxHp);

        public static string? BuildBanner(UnitIdentity? before, UnitIdentity? after)
        {
            if (before == null || after == null) return null;
            if (string.IsNullOrEmpty(before.Name) || string.IsNullOrEmpty(after.Name)) return null;
            if (before.Name == after.Name) return null;

            string beforeJob = string.IsNullOrEmpty(before.JobName) ? "?" : before.JobName!;
            string afterJob  = string.IsNullOrEmpty(after.JobName)  ? "?" : after.JobName!;

            return $"=== TURN HANDOFF: {before.Name}({beforeJob}) → {after.Name}({afterJob}) ({after.X},{after.Y}) HP={after.Hp}/{after.MaxHp} ===";
        }
    }
}
