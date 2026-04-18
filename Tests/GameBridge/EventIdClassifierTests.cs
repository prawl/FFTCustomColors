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

        // Session 37: mid-battle eventId classifier. Stricter upper bound (200)
        // because nameId aliases kick in during combat animations.

        [Theory]
        [InlineData(1, true)]      // lower bound
        [InlineData(100, true)]
        [InlineData(199, true)]    // upper bound (exclusive of 200)
        [InlineData(0, false)]
        [InlineData(200, false)]   // nameId-alias territory begins here
        [InlineData(303, false)]   // valid real event but NOT a mid-battle event
        [InlineData(399, false)]
        [InlineData(0xFFFF, false)]
        [InlineData(-1, false)]
        public void IsMidBattleEvent_BoundarySweep(int eventId, bool expected)
        {
            Assert.Equal(expected, ScreenDetectionLogic.IsMidBattleEvent(eventId));
        }

        [Fact]
        public void IsMidBattleEvent_Implies_IsRealEvent()
        {
            // Every mid-battle event is also a real event (narrower window).
            for (int e = 0; e < 500; e++)
            {
                if (ScreenDetectionLogic.IsMidBattleEvent(e))
                    Assert.True(ScreenDetectionLogic.IsRealEvent(e));
            }
        }

        [Fact]
        public void EventIdMidBattleMaxExclusive_IsBelowEventIdRealMax()
        {
            // Pin the invariant that the mid-battle bound is narrower than the
            // full-event bound. If someone widens MidBattle past Real, the
            // mid-battle rule becomes no-stricter-than the regular rule.
            Assert.True(ScreenDetectionLogic.EventIdMidBattleMaxExclusive
                < ScreenDetectionLogic.EventIdRealMaxExclusive);
        }
    }
}
