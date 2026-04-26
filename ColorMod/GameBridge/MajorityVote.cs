namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure helper: pick the majority of three string samples for the
    /// multi-sample screen-debounce. Null samples are treated as missing
    /// and don't vote. When no majority exists, prefer the latest non-
    /// null sample (newest read = most likely to reflect current truth).
    /// </summary>
    public static class MajorityVote
    {
        public static string? Pick(string? first, string? second, string? third)
        {
            // 2-of-3 agreements first.
            if (first != null && first == second) return first;
            if (first != null && first == third) return first;
            if (second != null && second == third) return second;
            // No agreement — return the freshest non-null sample.
            return third ?? second ?? first;
        }
    }
}
