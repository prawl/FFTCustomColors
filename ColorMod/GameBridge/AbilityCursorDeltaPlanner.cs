namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Decides when a counter-delta read after an Up/Down press can be
    /// trusted for ability-list navigation, and when to fall back to
    /// blind-count scrolling.
    ///
    /// Session 31 shipped a counter-delta approach via cursor counter at
    /// 0x140C0EB20. Worked fine on Down (monotonic), but Up-wrap produced
    /// negative deltas that exploded the retry math (observed: expected
    /// +3, got 0; expected +6, got -6; expected +9, got -24). Reverted.
    ///
    /// This class formalizes the "is this delta trustworthy" decision as
    /// a pure function. TODO §12 "Ability list navigation: use counter-delta
    /// instead of brute-force scroll".
    ///
    /// Trust rules:
    ///   1. Sign must match expected direction (Down=+1, Up=-1).
    ///   2. Magnitude must be &lt; listLength (wrap-around is suspect).
    ///   3. Magnitude must be nonzero (frozen counter is suspect).
    ///   4. If an expected magnitude is given, a wildly-off magnitude
    ///      (3x expected or more) falls back to blind.
    /// </summary>
    public static class AbilityCursorDeltaPlanner
    {
        public class Decision
        {
            /// <summary>True if the observed delta can be used to continue navigation.</summary>
            public bool TrustDelta { get; set; }
            /// <summary>Absolute number of key presses remaining when TrustDelta is true; 0 otherwise.</summary>
            public int RemainingKeys { get; set; }
        }

        /// <summary>
        /// Decide whether to trust an observed delta on a cursor counter.
        /// </summary>
        /// <param name="expectedDirection">+1 for Down, -1 for Up.</param>
        /// <param name="observedDelta">Counter value after press - value before.</param>
        /// <param name="listLength">Total rows in the ability list.</param>
        /// <param name="expectedMagnitude">Optional — how many presses were issued. If provided, deltas 3x or larger get treated as suspicious.</param>
        public static Decision Decide(
            int expectedDirection,
            int observedDelta,
            int listLength,
            int expectedMagnitude = -1)
        {
            if (listLength <= 0) return NoTrust();
            if (observedDelta == 0) return NoTrust();

            // Sign must match.
            if (expectedDirection > 0 && observedDelta < 0) return NoTrust();
            if (expectedDirection < 0 && observedDelta > 0) return NoTrust();

            int magnitude = observedDelta < 0 ? -observedDelta : observedDelta;

            // Wrap-around or overflow: magnitude equals-or-exceeds list length.
            if (magnitude >= listLength) return NoTrust();

            // Wildly-different magnitude vs expected → math exploded.
            if (expectedMagnitude > 0 && magnitude >= 3 * expectedMagnitude)
                return NoTrust();

            return new Decision { TrustDelta = true, RemainingKeys = magnitude };
        }

        private static Decision NoTrust() =>
            new() { TrustDelta = false, RemainingKeys = 0 };
    }
}
