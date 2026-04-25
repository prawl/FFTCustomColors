namespace FFTColorCustomizer.GameBridge
{
    public enum ReviveIntent
    {
        None,         // alive non-undead — no effect, hide from target list
        Revive,       // dead ally — canonical use, restore life
        ReviveEnemy,  // dead enemy — would resurrect them; surface with warning tag
        Ko,           // undead-status enemy — instant KO via reverse-revive
        KoAlly,       // undead-status ally — kills your own unit; surface with warning
    }

    /// <summary>
    /// Classify the intent of using a "Removes KO" / revive ability (Phoenix
    /// Down, Raise, Arise, Revive) on a specific target. Phoenix Down reverses
    /// life state — it's a kill move on Undead-status enemies, not just a
    /// revive. Without this disambiguation the shell renderer would either
    /// hide tactically valid kill targets or list useless tiles. Live-flagged
    /// 2026-04-25 playtest: agent saw PD listing dead enemies + petrified
    /// Dryad as targets and concluded PD was useless — missed the
    /// Skeleton-killer use case entirely.
    /// </summary>
    public static class ReviveTargetClassifier
    {
        // Status byte 0 bit 0x10 = Undead status (per StatusDecoder).
        private const byte UndeadBit = 0x10;
        // Status byte 0 bit 0x20 = Dead lifeState (per StatusDecoder).
        private const byte DeadBit = 0x20;

        public static ReviveIntent Classify(
            int targetTeam, int casterTeam, int targetHp, byte[]? targetStatusBytes)
        {
            bool isDead = (targetHp <= 0)
                || (targetStatusBytes != null
                    && targetStatusBytes.Length > 0
                    && (targetStatusBytes[0] & DeadBit) != 0);
            bool isUndead = targetStatusBytes != null
                && targetStatusBytes.Length > 0
                && (targetStatusBytes[0] & UndeadBit) != 0;

            // Friendliness: same team OR target team 2 (NPC guest) when
            // caster is on team 0 (player). Team 2 always counts as ally
            // for the player. Enemy-on-enemy revival isn't reachable in
            // practice (enemies don't carry PD); keep the rule symmetric.
            bool isFriendly = targetTeam == casterTeam
                || (casterTeam == 0 && targetTeam == 2)
                || (casterTeam == 2 && targetTeam == 0);

            // Dead state wins over Undead — it's the canonical life-state.
            // The (extremely unlikely) Dead+Undead combo classifies as a
            // revive: resurrect the corpse, the Undead bit goes with the
            // body, and the game treats the resulting unit as alive.
            if (isDead)
                return isFriendly ? ReviveIntent.Revive : ReviveIntent.ReviveEnemy;

            if (isUndead)
                return isFriendly ? ReviveIntent.KoAlly : ReviveIntent.Ko;

            return ReviveIntent.None;
        }
    }
}
