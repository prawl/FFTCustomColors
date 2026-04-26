namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// S60: pure HP-delta suffix formatter for battle_ability's response.Info.
    /// Mirrors the BattleAttack HIT/KO output so targeted abilities (Cure,
    /// Phoenix Down, Throw Stone, etc.) report damage/heal/KO/revive deltas
    /// in the same shape Claude already reads for basic attacks.
    ///
    /// Returns an empty string when the delta can't be measured — cast-time
    /// abilities, or pre/post HP reads that failed (negative sentinels).
    /// </summary>
    public static class AbilityHpDeltaFormatter
    {
        public static string Format(int preHp, int postHp, int maxHp, bool isRevive = false)
        {
            if (preHp < 0 || postHp < 0 || maxHp <= 0) return "";
            // Phantom-revive defense: if this is a revive ability AND the
            // target was dead (preHp=0) AND is still dead (postHp=0), the
            // revive didn't land. Without this flag the helper returns ""
            // and the caller sees `Used Phoenix Down on (X,Y)` with no
            // way to tell it was a no-op. Live-flagged 2026-04-26 P3.
            if (preHp == 0 && postHp == 0 && isRevive)
                return $" — REVIVE FAILED (target still 0/{maxHp})";
            if (preHp == postHp) return "";
            if (preHp > 0 && postHp == 0)
                return $" — KO'd! ({preHp}→0/{maxHp})";
            if (preHp == 0 && postHp > 0)
                return $" — revived (0→{postHp}/{maxHp})";
            return $" ({preHp}→{postHp}/{maxHp})";
        }
    }
}
