namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Idempotent prepend of the multi-unit `=== TURN HANDOFF: A → B ===`
    /// banner to a response.Info field. Skips when the banner is already
    /// present in Info (sub-step like battle_wait may have prepended it
    /// first; ExecuteTurn's outer prepend would otherwise produce a dupe
    /// — observed live 2026-04-25 playtest).
    /// </summary>
    public static class HandoffBannerJoiner
    {
        public static string? PrependIfAbsent(string? info, string? banner)
        {
            if (string.IsNullOrEmpty(banner)) return info;
            if (string.IsNullOrEmpty(info)) return banner;
            // Exact-match dedupe: the same banner string already appears
            // anywhere in Info (start, middle, end). Different banner
            // content (e.g. different units) means a genuinely new event
            // and gets prepended normally.
            if (info!.Contains(banner!)) return info;
            return banner + " | " + info;
        }
    }
}
