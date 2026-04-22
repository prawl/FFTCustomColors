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

        public (int move, int jump)? Get(int maxHp)
        {
            return _entries.TryGetValue(maxHp, out var v) ? v : null;
        }

        public void Put(int maxHp, int move, int jump)
        {
            // Same sanity check TryMoveJumpMatches uses — keep cache clean.
            if (move < 1 || move > 10 || jump < 1 || jump > 8) return;
            _entries[maxHp] = (move, jump);
        }

        public void Clear()
        {
            _entries.Clear();
        }
    }
}
