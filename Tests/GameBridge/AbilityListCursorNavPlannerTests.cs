using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for <see cref="AbilityListCursorNavPlanner"/> — given the
    /// current cursor index (read live from heap byte 0x1314DF920 + 3
    /// mirrors, cracked session 55) and the target ability index, decide
    /// the optimal direction (Down or Up) and key-press count to navigate
    /// to target. Wraps both ways: a 15-item list with cursor at 14 going
    /// to target 0 should pick Down×1 (wrap), not Up×14.
    /// </summary>
    public class AbilityListCursorNavPlannerTests
    {
        [Fact]
        public void SameIndex_NoPressesNeeded()
        {
            var plan = AbilityListCursorNavPlanner.Plan(
                currentIndex: 5, targetIndex: 5, listSize: 12);
            Assert.Equal(0, plan.PressCount);
            Assert.Equal(AbilityListCursorNavPlanner.Direction.None, plan.Direction);
        }

        [Fact]
        public void TargetOneBelow_OneDown()
        {
            var plan = AbilityListCursorNavPlanner.Plan(
                currentIndex: 0, targetIndex: 1, listSize: 12);
            Assert.Equal(1, plan.PressCount);
            Assert.Equal(AbilityListCursorNavPlanner.Direction.Down, plan.Direction);
        }

        [Fact]
        public void TargetOneAbove_OneUp()
        {
            var plan = AbilityListCursorNavPlanner.Plan(
                currentIndex: 5, targetIndex: 4, listSize: 12);
            Assert.Equal(1, plan.PressCount);
            Assert.Equal(AbilityListCursorNavPlanner.Direction.Up, plan.Direction);
        }

        [Fact]
        public void ForwardCloserThanBackward_ChoosesDown()
        {
            // 15-item list, cursor=2, target=5. Down×3 vs Up×12 → Down.
            var plan = AbilityListCursorNavPlanner.Plan(
                currentIndex: 2, targetIndex: 5, listSize: 15);
            Assert.Equal(3, plan.PressCount);
            Assert.Equal(AbilityListCursorNavPlanner.Direction.Down, plan.Direction);
        }

        [Fact]
        public void BackwardCloserThanForward_ChoosesUp()
        {
            // 15-item list, cursor=12, target=2. Down×5 (wrap 12→13→14→0→1→2)
            // vs Up×10. Down wins (5 < 10).
            var plan = AbilityListCursorNavPlanner.Plan(
                currentIndex: 12, targetIndex: 2, listSize: 15);
            Assert.Equal(5, plan.PressCount);
            Assert.Equal(AbilityListCursorNavPlanner.Direction.Down, plan.Direction);
        }

        [Fact]
        public void ForwardWrapShorterThanBackward_ChoosesDown()
        {
            // 15-item list, cursor=14, target=0. Down×1 (wrap) vs Up×14.
            // Down wins.
            var plan = AbilityListCursorNavPlanner.Plan(
                currentIndex: 14, targetIndex: 0, listSize: 15);
            Assert.Equal(1, plan.PressCount);
            Assert.Equal(AbilityListCursorNavPlanner.Direction.Down, plan.Direction);
        }

        [Fact]
        public void BackwardWrapShorterThanForward_ChoosesUp()
        {
            // 15-item list, cursor=0, target=14. Up×1 (wrap) vs Down×14.
            // Up wins.
            var plan = AbilityListCursorNavPlanner.Plan(
                currentIndex: 0, targetIndex: 14, listSize: 15);
            Assert.Equal(1, plan.PressCount);
            Assert.Equal(AbilityListCursorNavPlanner.Direction.Up, plan.Direction);
        }

        [Fact]
        public void ExactHalfAround_PrefersDown()
        {
            // Tie-breaker: 12-item list, cursor=0, target=6. Down×6 vs Up×6.
            // Tie — pick Down (arbitrary but deterministic; Down is the more
            // common direction so it minimizes "weird-feeling" Up navigations).
            var plan = AbilityListCursorNavPlanner.Plan(
                currentIndex: 0, targetIndex: 6, listSize: 12);
            Assert.Equal(6, plan.PressCount);
            Assert.Equal(AbilityListCursorNavPlanner.Direction.Down, plan.Direction);
        }

        [Fact]
        public void TwoItemList_ToggleAlwaysDown()
        {
            // 2-item list, cursor=0, target=1. Down=1, Up=1. Tie → Down.
            var plan = AbilityListCursorNavPlanner.Plan(
                currentIndex: 0, targetIndex: 1, listSize: 2);
            Assert.Equal(1, plan.PressCount);
            Assert.Equal(AbilityListCursorNavPlanner.Direction.Down, plan.Direction);
        }

        [Fact]
        public void InvalidListSize_NoPlan()
        {
            // Defensive: listSize 0 is impossible (we wouldn't be in a list
            // with no abilities) but the planner shouldn't crash.
            var plan = AbilityListCursorNavPlanner.Plan(
                currentIndex: 0, targetIndex: 0, listSize: 0);
            Assert.Equal(0, plan.PressCount);
            Assert.Equal(AbilityListCursorNavPlanner.Direction.None, plan.Direction);
        }

        [Fact]
        public void OutOfRangeIndices_NoPlan()
        {
            // Defensive: caller bug surfaces as a no-op rather than an
            // unbounded loop.
            var plan = AbilityListCursorNavPlanner.Plan(
                currentIndex: -1, targetIndex: 5, listSize: 10);
            Assert.Equal(AbilityListCursorNavPlanner.Direction.None, plan.Direction);

            plan = AbilityListCursorNavPlanner.Plan(
                currentIndex: 5, targetIndex: 10, listSize: 10);
            Assert.Equal(AbilityListCursorNavPlanner.Direction.None, plan.Direction);
        }

        [Fact]
        public void LongList_ChoosesShorterDirection()
        {
            // 30-item list, cursor=2, target=29. Down×27 vs Up×3 (wrap).
            // Up wins.
            var plan = AbilityListCursorNavPlanner.Plan(
                currentIndex: 2, targetIndex: 29, listSize: 30);
            Assert.Equal(3, plan.PressCount);
            Assert.Equal(AbilityListCursorNavPlanner.Direction.Up, plan.Direction);
        }
    }
}
