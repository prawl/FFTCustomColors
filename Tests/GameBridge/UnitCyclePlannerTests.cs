using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for <see cref="UnitCyclePlanner.Plan"/> — pure planner for the
    /// `swap_unit_to &lt;name&gt;` helper. Given current + target displayOrder
    /// on a ring of size N, returns the shortest Q/E key sequence.
    /// Q = previous (-1, wraps), E = next (+1, wraps).
    ///
    /// Session 47: TODO §10.6 EquipmentAndAbilities helpers entry.
    /// </summary>
    public class UnitCyclePlannerTests
    {
        [Fact]
        public void SameUnit_ReturnsEmptySequence()
        {
            var plan = UnitCyclePlanner.Plan(fromIndex: 3, toIndex: 3, rosterCount: 14);
            Assert.Empty(plan.Keys);
        }

        [Fact]
        public void OneStepForward_UsesE()
        {
            var plan = UnitCyclePlanner.Plan(fromIndex: 0, toIndex: 1, rosterCount: 14);
            Assert.Equal(new[] { 'E' }, plan.Keys);
        }

        [Fact]
        public void OneStepBackward_UsesQ()
        {
            var plan = UnitCyclePlanner.Plan(fromIndex: 5, toIndex: 4, rosterCount: 14);
            Assert.Equal(new[] { 'Q' }, plan.Keys);
        }

        [Fact]
        public void LongDistance_ChoosesShorterDirection()
        {
            // 14-unit roster: from 0 to 10. Forward = 10 steps; backward = 4
            // steps (wrap 0→13→12→11→10). Planner picks backward.
            var plan = UnitCyclePlanner.Plan(fromIndex: 0, toIndex: 10, rosterCount: 14);
            Assert.Equal(4, plan.Keys.Length);
            Assert.All(plan.Keys, k => Assert.Equal('Q', k));
        }

        [Fact]
        public void LongDistance_ForwardWhenShorter()
        {
            // 14-unit roster: from 0 to 3. Forward = 3; backward = 11.
            var plan = UnitCyclePlanner.Plan(fromIndex: 0, toIndex: 3, rosterCount: 14);
            Assert.Equal(3, plan.Keys.Length);
            Assert.All(plan.Keys, k => Assert.Equal('E', k));
        }

        [Fact]
        public void WrapsAroundEnd_Forward()
        {
            // From 13 to 0 on a 14-unit roster: one E wraps to 0.
            var plan = UnitCyclePlanner.Plan(fromIndex: 13, toIndex: 0, rosterCount: 14);
            Assert.Equal(new[] { 'E' }, plan.Keys);
        }

        [Fact]
        public void WrapsAroundStart_Backward()
        {
            // From 0 to 13: one Q wraps to 13.
            var plan = UnitCyclePlanner.Plan(fromIndex: 0, toIndex: 13, rosterCount: 14);
            Assert.Equal(new[] { 'Q' }, plan.Keys);
        }

        [Fact]
        public void HalfwayAround_PrefersForward()
        {
            // Even roster, exactly-halfway: forward and backward tie. Planner
            // prefers forward (E) as tie-breaker — matches in-game scanning
            // direction when the user doesn't care.
            var plan = UnitCyclePlanner.Plan(fromIndex: 0, toIndex: 7, rosterCount: 14);
            Assert.Equal(7, plan.Keys.Length);
            Assert.All(plan.Keys, k => Assert.Equal('E', k));
        }

        [Fact]
        public void InvalidRosterCount_ReturnsEmpty()
        {
            // Guard against divide-by-zero or negative counts. Empty sequence
            // means "no safe action" — caller should error up the stack.
            Assert.Empty(UnitCyclePlanner.Plan(0, 1, 0).Keys);
            Assert.Empty(UnitCyclePlanner.Plan(0, 1, -1).Keys);
        }

        [Fact]
        public void TargetOutOfRange_ReturnsEmpty()
        {
            Assert.Empty(UnitCyclePlanner.Plan(fromIndex: 0, toIndex: 99, rosterCount: 14).Keys);
            Assert.Empty(UnitCyclePlanner.Plan(fromIndex: 0, toIndex: -1, rosterCount: 14).Keys);
        }

        [Fact]
        public void FromOutOfRange_ReturnsEmpty()
        {
            Assert.Empty(UnitCyclePlanner.Plan(fromIndex: -1, toIndex: 3, rosterCount: 14).Keys);
            Assert.Empty(UnitCyclePlanner.Plan(fromIndex: 14, toIndex: 3, rosterCount: 14).Keys);
        }

        [Fact]
        public void SmallRoster_Works()
        {
            // 3 unit roster: from 2 to 0. Forward = 1 (wrap); backward = 2.
            var plan = UnitCyclePlanner.Plan(fromIndex: 2, toIndex: 0, rosterCount: 3);
            Assert.Equal(new[] { 'E' }, plan.Keys);
        }
    }
}
