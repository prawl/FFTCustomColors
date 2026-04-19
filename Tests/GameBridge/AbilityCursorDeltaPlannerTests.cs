using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for <see cref="AbilityCursorDeltaPlanner"/> — decides when a
    /// counter-delta read after a Up/Down press can be trusted, and when
    /// to fall back to blind-count. Session 31 attempt shipped counter-delta
    /// but broke Lloyd's Jump targeting because Up-wrap gave negative deltas
    /// that exploded retry math. This planner formalizes the decision.
    ///
    /// Rule: trust delta only when sign matches expected direction AND
    /// magnitude is reasonable (≤ list length). Otherwise blind fallback.
    ///
    /// TODO §12 "Ability list navigation: use counter-delta instead of
    /// brute-force scroll".
    /// </summary>
    public class AbilityCursorDeltaPlannerTests
    {
        [Fact]
        public void PositiveDelta_WhenPressingDown_TrustsDelta()
        {
            // Pressed Down 3 times; counter went 0→3. Delta=+3 matches
            // expected direction (down is positive counter motion).
            var plan = AbilityCursorDeltaPlanner.Decide(
                expectedDirection: +1, observedDelta: +3,
                listLength: 10);
            Assert.True(plan.TrustDelta);
            Assert.Equal(3, plan.RemainingKeys);
        }

        [Fact]
        public void NegativeDelta_WhenPressingUp_TrustsDelta()
        {
            // Pressed Up 2 times; counter went 5→3. Delta=-2 matches Up.
            var plan = AbilityCursorDeltaPlanner.Decide(
                expectedDirection: -1, observedDelta: -2,
                listLength: 10);
            Assert.True(plan.TrustDelta);
            Assert.Equal(2, plan.RemainingKeys);
        }

        [Fact]
        public void NegativeDeltaOnUpWrap_FallsBackToBlind()
        {
            // The session 31 bug: pressed Up×3 expecting -3, but the counter
            // wrapped. On an 8-item list, 0→7 after Up-wrap gives +7 or -1
            // depending on the widget's counter semantics. Our rule:
            // magnitude much larger than expected → don't trust.
            var plan = AbilityCursorDeltaPlanner.Decide(
                expectedDirection: -1, observedDelta: +7,
                listLength: 8,
                expectedMagnitude: 3);
            Assert.False(plan.TrustDelta);
        }

        [Fact]
        public void Zero_Delta_FallsBackToBlind()
        {
            // Widget froze or counter not tracking. Don't infer anything.
            var plan = AbilityCursorDeltaPlanner.Decide(
                expectedDirection: +1, observedDelta: 0,
                listLength: 10,
                expectedMagnitude: 5);
            Assert.False(plan.TrustDelta);
        }

        [Fact]
        public void SignMismatch_FallsBackToBlind()
        {
            // Pressed Down expecting +, got negative. Counter not tracking
            // the right axis. Blind fallback.
            var plan = AbilityCursorDeltaPlanner.Decide(
                expectedDirection: +1, observedDelta: -2,
                listLength: 10);
            Assert.False(plan.TrustDelta);
        }

        [Fact]
        public void MagnitudeWayOverListLength_FallsBackToBlind()
        {
            // Counter returned +24 when list has 8 items. Math exploded —
            // don't use the value.
            var plan = AbilityCursorDeltaPlanner.Decide(
                expectedDirection: +1, observedDelta: +24,
                listLength: 8);
            Assert.False(plan.TrustDelta);
        }

        [Fact]
        public void MagnitudeEqualsListLength_StillFallsBack()
        {
            // Boundary: delta = list length (full wrap-around) is suspect.
            var plan = AbilityCursorDeltaPlanner.Decide(
                expectedDirection: +1, observedDelta: +10,
                listLength: 10);
            Assert.False(plan.TrustDelta);
        }

        [Fact]
        public void MagnitudeOneUnderListLength_StillTrusted()
        {
            // Moving from row 0 to last row (9 on a 10-row list) is legit.
            var plan = AbilityCursorDeltaPlanner.Decide(
                expectedDirection: +1, observedDelta: +9,
                listLength: 10);
            Assert.True(plan.TrustDelta);
            Assert.Equal(9, plan.RemainingKeys);
        }

        [Fact]
        public void TrustDelta_RemainingKeysIsAbsDelta()
        {
            // Whether expected sign is +1 or -1, RemainingKeys is always
            // the absolute delta — it's a positive count of key presses.
            var plan1 = AbilityCursorDeltaPlanner.Decide(+1, +5, 10);
            var plan2 = AbilityCursorDeltaPlanner.Decide(-1, -5, 10);
            Assert.Equal(5, plan1.RemainingKeys);
            Assert.Equal(5, plan2.RemainingKeys);
        }

        [Fact]
        public void EmptyList_FallsBack()
        {
            var plan = AbilityCursorDeltaPlanner.Decide(
                expectedDirection: +1, observedDelta: +3,
                listLength: 0);
            Assert.False(plan.TrustDelta);
        }
    }
}
