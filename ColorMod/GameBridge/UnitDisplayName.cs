namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure helper: resolve the best available human-readable label for
    /// a battle unit given its Name (story/player units) and JobName
    /// (fallback for enemies whose names aren't readable from memory).
    /// Used by stat-tracker hooks so enemy-side kills attribute to
    /// "Minotaur" instead of literal "(unknown)".
    /// </summary>
    public static class UnitDisplayName
    {
        public const string Unknown = "(unknown)";

        public static string For(string? name, string? jobName)
        {
            if (!string.IsNullOrWhiteSpace(name)) return name!;
            if (!string.IsNullOrWhiteSpace(jobName)) return jobName!;
            return Unknown;
        }
    }
}
