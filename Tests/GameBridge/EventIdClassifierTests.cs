using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for the eventId classification helpers. ScreenDetectionLogic has
    /// repeatedly open-coded the range check `eventId >= 1 && eventId < 400
    /// && eventId != 0xFFFF` and the inverse `eventId == 0 || eventId == 0xFFFF`
    /// across multiple rules. These helpers centralize the definition so a
    /// future range tweak only needs to change one place.
    ///
    /// Invariants:
    ///   IsRealEvent(e) == true  iff  1 &lt;= e &lt; 400 AND e != 0xFFFF
    ///   IsRealEvent(e) == false  iff  !IsRealEvent(e) (i.e. exhaustive)
    /// </summary>
    public class EventIdClassifierTests
    {
        [Theory]
        [InlineData(1, true)]      // lower bound
        [InlineData(2, true)]
        [InlineData(200, true)]
        [InlineData(303, true)]    // session 21 Orbonne battle
        [InlineData(399, true)]    // upper bound (exclusive of 400)
        [InlineData(0, false)]     // unset sentinel
        [InlineData(400, false)]   // past upper bound (nameIds start here)
        [InlineData(500, false)]
        [InlineData(0xFFFF, false)] // other unset sentinel
        [InlineData(-1, false)]    // negative not valid
        public void IsRealEvent_BoundarySweep(int eventId, bool expected)
        {
            Assert.Equal(expected, ScreenDetectionLogic.IsRealEvent(eventId));
        }

        [Theory]
        [InlineData(0, true)]       // the two canonical unset values
        [InlineData(0xFFFF, true)]
        [InlineData(1, false)]      // anything in the real range is not unset
        [InlineData(399, false)]
        [InlineData(400, false)]    // out-of-range but not unset
        [InlineData(-1, false)]     // negative is invalid, not unset
        public void IsEventIdUnset_Sweep(int eventId, bool expected)
        {
            Assert.Equal(expected, ScreenDetectionLogic.IsEventIdUnset(eventId));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(0xFFFF)]
        [InlineData(1)]
        [InlineData(399)]
        [InlineData(400)]
        public void IsRealEvent_AndIsUnset_AreNotBothTrue(int eventId)
        {
            // The two classifiers are disjoint — never both true for the same input.
            bool real = ScreenDetectionLogic.IsRealEvent(eventId);
            bool unset = ScreenDetectionLogic.IsEventIdUnset(eventId);
            Assert.False(real && unset);
        }
    }
}
