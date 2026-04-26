namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Items abilities (Potion, Phoenix Down, Antidote, etc.) have R=4 in
    /// the canonical metadata because that's their range when used by a
    /// Chemist (Items as primary). When a non-Chemist runs Items as
    /// SECONDARY, the items nerf to R=1 (adjacent only) UNLESS the unit
    /// has the "Throw Items" support ability (which restores R=4).
    ///
    /// <para>Live-flagged 2026-04-26 playtest at Siedge Weald: Ramza
    /// (Knight, Items secondary, no Throw Items) saw the bridge surface
    /// Phoenix Down `R:4 → (6,0)&lt;Skeleton [KO]&gt;` at d=3 and the
    /// action consumed without effect because the actual in-game range
    /// was 1. This adjuster mirrors the engine's rule so target tiles
    /// reflect what the game will accept.</para>
    /// </summary>
    public static class ItemRangeAdjuster
    {
        public const string ItemsSkillsetName = "Items";
        public const string ThrowItemsSupportName = "Throw Items";
        public const string NerfedRange = "1";

        public static string Adjust(
            string skillsetName,
            string originalHRange,
            string? primarySkillset,
            string? supportAbility)
        {
            // Only the Items skillset has this range nerf.
            if (skillsetName != ItemsSkillsetName) return originalHRange;
            // Self-targeted abilities aren't ranged; leave them alone.
            if (originalHRange == "Self") return originalHRange;
            // Chemist (Items as primary): full R=4 applies.
            if (primarySkillset == ItemsSkillsetName) return originalHRange;
            // Throw Items support restores the Chemist range to a non-Chemist.
            if (supportAbility == ThrowItemsSupportName) return originalHRange;
            // Default: Items as secondary without Throw Items → R=1.
            return NerfedRange;
        }
    }
}
