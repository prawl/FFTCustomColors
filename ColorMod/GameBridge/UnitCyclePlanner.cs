namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure planner for Q/E roster cycling on PartyMenu nested screens
    /// (CharacterStatus / EqA / JobSelection / pickers). Given current +
    /// target displayOrder on a ring of size N, emits the shortest key
    /// sequence. Q = previous (-1, wraps), E = next (+1, wraps).
    ///
    /// Use case: `swap_unit_to &lt;name&gt;` helper. Caller resolves the target
    /// name to a displayOrder via the roster, then feeds the pair here and
    /// dispatches the returned keys through the existing key sender.
    ///
    /// Session 47. Pure — no game state consulted.
    /// </summary>
    public static class UnitCyclePlanner
    {
        /// <summary>
        /// Plan for a Q/E cycle operation.
        /// </summary>
        public class Result
        {
            public char[] Keys { get; set; } = System.Array.Empty<char>();
        }

        /// <summary>
        /// Returns the shortest Q/E sequence to move the cursor from
        /// <paramref name="fromIndex"/> to <paramref name="toIndex"/> on a
        /// ring of <paramref name="rosterCount"/> units. Invalid inputs
        /// (out-of-range indices, non-positive count) return an empty
        /// sequence — the caller should treat that as "can't plan, error."
        /// Ties (exactly halfway) prefer forward (E).
        /// </summary>
        public static Result Plan(int fromIndex, int toIndex, int rosterCount)
        {
            var result = new Result();
            if (rosterCount <= 0) return result;
            if (fromIndex < 0 || fromIndex >= rosterCount) return result;
            if (toIndex < 0 || toIndex >= rosterCount) return result;
            if (fromIndex == toIndex) return result;

            int forward = (toIndex - fromIndex + rosterCount) % rosterCount;
            int backward = rosterCount - forward;

            // Tie → forward (E). Otherwise whichever is shorter.
            char direction = forward <= backward ? 'E' : 'Q';
            int count = forward <= backward ? forward : backward;

            var keys = new char[count];
            for (int i = 0; i < count; i++) keys[i] = direction;
            result.Keys = keys;
            return result;
        }
    }
}
