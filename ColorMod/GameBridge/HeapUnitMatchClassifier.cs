namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Scores a heap unit-struct candidate's likelihood of being the real
    /// struct for a given (expectedLevel) pair. When multiple heap slots
    /// match a unit's (HP, MaxHP) pattern — common for low-HP units — we
    /// want to prefer the one whose level byte agrees with the scanned
    /// level. Real unit structs carry the level at +0x09; false-positive
    /// bytes can carry anything.
    ///
    /// Returns higher = more confident. Callers pick the highest-scoring
    /// candidate, falling back to first-match when all candidates tie.
    /// </summary>
    public static class HeapUnitMatchClassifier
    {
        private const int ExactMatchScore = 100;
        private const int InRangeMismatchScore = 20;
        private const int OutOfRangeScore = 0;
        private const int UnknownExpectedScore = 50;

        public static int Score(int candidateLevel, int expectedLevel)
        {
            bool inRange = candidateLevel >= 1 && candidateLevel <= 99;
            if (expectedLevel == 0)
            {
                // Caller doesn't know the expected level — treat all
                // candidates equally so the caller can fall back to
                // first-match behavior.
                return UnknownExpectedScore;
            }
            if (!inRange) return OutOfRangeScore;
            if (candidateLevel == expectedLevel) return ExactMatchScore;
            return InRangeMismatchScore;
        }
    }
}
