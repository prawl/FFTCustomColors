using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Per-battle memo of last-successful <see cref="RosterMatcher"/>
    /// results, keyed by the unit's stable <c>NameId</c>. Active-unit
    /// reads expose NameId from memory directly (separate from the roster
    /// slot lookup), so even when a level-up shifts the scanned Level
    /// out of sync with the roster slot's Level for a frame, we can
    /// still recover the last-known Job / Secondary / Brave / Faith for
    /// that unit.
    ///
    /// <para>2026-04-26 Mandalia: Ramza levelled up mid-battle, scanned
    /// Level=8 vs roster slot Level=7 (lag), RosterMatcher returned
    /// NameId=0 → unit.Job collapsed to default 0 (Squire) → bridge
    /// offered Mettle abilities and menu navigation desynchronised.</para>
    ///
    /// <para>Caller invalidates on battle-boundary transitions; within a
    /// battle, NameId is the stable identity (only the slot-level stats
    /// can lag a frame after a level-up).</para>
    /// </summary>
    public class RosterMatchCache
    {
        private readonly Dictionary<int, RosterMatchResult> _entries = new();

        public RosterMatchResult? Get(int nameId)
        {
            return _entries.TryGetValue(nameId, out var v)
                ? v
                : (RosterMatchResult?)null;
        }

        public void Put(int nameId, RosterMatchResult match)
        {
            // Sentinel rejection: NameId=0 means "no match" upstream.
            if (nameId <= 0) return;
            // Defensive: key/payload must agree; otherwise the cache is
            // poisoned and a future Get returns wrong identity.
            if (match.NameId != nameId) return;
            _entries[nameId] = match;
        }

        public void Clear()
        {
            _entries.Clear();
        }
    }
}
