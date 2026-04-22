namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure planner for navigating an ability-list cursor (e.g.
    /// BattleWhiteMagicks, BattleBlackMagicks) from the current index to a
    /// target index. Picks the shorter of forward (Down) or backward (Up)
    /// path, accounting for list wrap.
    ///
    /// Pairs with the heap byte at 0x1314DF920 (cracked session 55) which
    /// holds the live cursor index. Replaces the old blind reset
    /// "Up×(listSize+1) then Down×targetIdx" pattern that landed at the
    /// last entry instead of index 0 because Up wraps from 0 to last.
    /// </summary>
    public static class AbilityListCursorNavPlanner
    {
        public enum Direction
        {
            None,
            Down,
            Up,
        }

        public class NavPlan
        {
            public Direction Direction { get; set; }
            public int PressCount { get; set; }
        }

        /// <summary>
        /// Compute the optimal direction + press count to walk the cursor
        /// from <paramref name="currentIndex"/> to <paramref name="targetIndex"/>
        /// in a list of <paramref name="listSize"/> entries (wraps both ways).
        /// Returns Direction.None / PressCount=0 when no navigation is needed
        /// or the inputs are invalid (out of range, empty list).
        /// Tie-breaker: when Down and Up cost the same, picks Down.
        /// </summary>
        public static NavPlan Plan(int currentIndex, int targetIndex, int listSize)
        {
            if (listSize <= 0) return Idle();
            if (currentIndex < 0 || currentIndex >= listSize) return Idle();
            if (targetIndex < 0 || targetIndex >= listSize) return Idle();
            if (currentIndex == targetIndex) return Idle();

            int forward = (targetIndex - currentIndex + listSize) % listSize;
            int backward = (currentIndex - targetIndex + listSize) % listSize;

            // Down wins ties — Down is the more common nav direction in
            // top-down ability lists, so a tied path feels less surprising
            // when we go Down.
            if (forward <= backward)
                return new NavPlan { Direction = Direction.Down, PressCount = forward };
            return new NavPlan { Direction = Direction.Up, PressCount = backward };
        }

        private static NavPlan Idle() =>
            new() { Direction = Direction.None, PressCount = 0 };
    }
}
