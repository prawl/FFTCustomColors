using System;
using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Per-battle cache of live-HP candidate addresses keyed by (maxHp, level).
    ///
    /// The HP table for a unit lives in read-only heap pages at addresses that
    /// are stable within a battle but shift on each new battle allocation.
    /// ReadLiveHp's full ~500MB SearchBytesInAllMemory takes ~100-200ms per
    /// call. Most attacks in a fight hit the same targets repeatedly — so
    /// caching the found addresses and revalidating with a 2-byte read on
    /// subsequent calls cuts ReadLiveHp latency to single-digit ms on hits.
    ///
    /// Caller's responsibility:
    ///   - Call Clear() on battle-start boundary (addresses relocate).
    ///   - Call Invalidate(k) when cached addresses produce nonsense reads
    ///     (e.g. value &gt; maxHp) so the next call re-scans.
    /// </summary>
    public class LiveHpAddressCache
    {
        private readonly Dictionary<(int maxHp, int level), List<IntPtr>> _entries
            = new();

        public List<IntPtr>? GetCachedAddresses(int maxHp, int level)
        {
            return _entries.TryGetValue((maxHp, level), out var list) ? list : null;
        }

        public void Remember(int maxHp, int level, IntPtr address)
        {
            if (!_entries.TryGetValue((maxHp, level), out var list))
            {
                list = new List<IntPtr>();
                _entries[(maxHp, level)] = list;
            }
            if (!list.Contains(address))
                list.Add(address);
        }

        public void Invalidate(int maxHp, int level)
        {
            _entries.Remove((maxHp, level));
        }

        public void Clear()
        {
            _entries.Clear();
        }
    }
}
