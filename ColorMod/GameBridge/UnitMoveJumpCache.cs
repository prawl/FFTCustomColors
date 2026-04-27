using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Per-battle memo of last-successful heap Move/Jump reads, keyed by
    /// MaxHp (same key the underlying search pattern uses). Fallback when
    /// <c>TryReadMoveJumpFromHeap</c> misses — preserves known-good values
    /// across frame-level search failures so scan_move doesn't collapse to
    /// Mv=0 Jmp=0 for units already seen this battle.
    ///
    /// Caller invalidates on battle-boundary transitions (StartBattle hook);
    /// within a battle, MaxHp is a stable key because unit stats don't
    /// change mid-battle (only Current HP does).
    /// </summary>
    public class UnitMoveJumpCache
    {
        private readonly Dictionary<int, (int move, int jump)> _entries = new();
        private (int move, int jump)? _mostRecent;

        public (int move, int jump)? Get(int maxHp)
        {
            return _entries.TryGetValue(maxHp, out var v) ? v : null;
        }

        // 2026-04-26: when the keyed lookup misses (e.g. unit levelled up
        // and MaxHp shifted), fall back to the most-recent successful Put.
        // Put is only called from the active-unit heap-read path, so the
        // most-recent entry is always the active unit's last-known-good
        // Move/Jump. Mv/Jp don't typically change at level-up (Movement+1
        // is a job-ability bonus, not a stat-up), so this fallback is
        // load-bearing for the post-level-up case where the heap struct
        // can't be re-located via the new MaxHp pattern.
        public (int move, int jump)? GetMostRecent() => _mostRecent;

        public void Put(int maxHp, int move, int jump)
        {
            // Same sanity check TryMoveJumpMatches uses — keep cache clean.
            if (move < 1 || move > 10 || jump < 1 || jump > 8) return;
            _entries[maxHp] = (move, jump);
            _mostRecent = (move, jump);
        }

        public void Clear()
        {
            _entries.Clear();
            _mostRecent = null;
        }
    }
}
