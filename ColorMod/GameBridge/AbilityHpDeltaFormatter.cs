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
        public static string Format(int preHp, int postHp, int maxHp)
        {
            if (preHp < 0 || postHp < 0 || maxHp <= 0) return "";
            if (preHp == postHp) return "";
            if (preHp > 0 && postHp == 0)
                return $" — KO'd! ({preHp}→0/{maxHp})";
            if (preHp == 0 && postHp > 0)
                return $" — revived (0→{postHp}/{maxHp})";
            return $" ({preHp}→{postHp}/{maxHp})";
        }
    }
}
