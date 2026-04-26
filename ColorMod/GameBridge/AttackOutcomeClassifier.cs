namespace FFTColorCustomizer.GameBridge
{
    public enum AttackOutcome
    {
        Hit,
        Miss,
        Ko,
        Unknown
    }

    /// <summary>
    /// Classify the result of a basic-Attack confirm using the post-animation
    /// screen state as the authoritative signal, with HP delta as a secondary
    /// detail. The heap-search ReadLiveHp helper can fail to locate a
    /// fingerprint-matched live copy of the target's HP and fall back to
    /// preHp, which previously caused the bridge to mis-report genuine hits
    /// as MISSED. The game's own UI advance is more reliable: HIT advances
    /// to BattleMoving (facing confirm), MISS re-opens BattleAttacking for
    /// re-targeting, and BattleVictory means the attack KO'd the last enemy.
    /// </summary>
    public static class AttackOutcomeClassifier
    {
        public static AttackOutcome Classify(
            string? postScreenName,
            int preHp,
            int postHp,
            bool targetMissingFromPostScan = false)
        {
            bool postHpReadable = postHp >= 0;
            bool isKoFromHp = postHpReadable && postHp <= 0;

            // Authoritative screen-state signals first — but cross-check
            // against HP evidence. Live-flagged 2026-04-26 (TWICE):
            // Ramza KO'd a Goblin and damaged a Skeleton but the screen
            // detector briefly caught BattleAttacking post-animation in
            // both cases, producing false MISSED reports. A real miss
            // leaves the target at full HP; ANY HP decrease (damage or
            // KO) means the attack landed and the screen flicker was
            // mid-transition.
            if (postScreenName == "BattleAttacking")
            {
                if (isKoFromHp) return AttackOutcome.Ko;
                if (postHpReadable && postHp < preHp) return AttackOutcome.Hit;
                // Target's HP isn't readable (heap-search failed AND static
                // array tile cleared) AND they had HP before — most likely
                // they died and the engine recycled their struct between
                // the attack and our read. Caller passes
                // targetMissingFromPostScan when both reads come up empty.
                if (targetMissingFromPostScan && preHp > 0) return AttackOutcome.Ko;
                return AttackOutcome.Miss;
            }

            // BattleVictory needs HP=0 corroboration. The screen detector
            // can flicker-touch BattleVictory mid-attack (live-observed at
            // Siedge Weald 2026-04-25 with the target still alive) and a
            // bare BattleVictory signal would mis-report a hit as KO. With
            // postHp >= 0 we trust the read; with postHp < 0 (read failed)
            // we degrade to Hit and let the response.Screen surface the
            // banner if the victory was real.
            if (postScreenName == "BattleVictory")
            {
                return isKoFromHp ? AttackOutcome.Ko : AttackOutcome.Hit;
            }

            if (postScreenName == "BattleMoving" || postScreenName == "BattleActing")
            {
                return isKoFromHp ? AttackOutcome.Ko : AttackOutcome.Hit;
            }

            // GameOver mid-attack is too ambiguous to classify (could be a
            // counter that landed after the attack).
            if (postScreenName == "GameOver") return AttackOutcome.Unknown;

            // No recognized screen signal — fall back to HP delta, but only
            // the affirmative direction. Equal HP is Unknown, NOT Miss; that
            // was the prior bug.
            if (isKoFromHp) return AttackOutcome.Ko;
            if (postHpReadable && postHp != preHp) return AttackOutcome.Hit;
            return AttackOutcome.Unknown;
        }
    }
}
