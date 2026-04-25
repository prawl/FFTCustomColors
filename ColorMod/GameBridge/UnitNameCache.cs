using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Caches unit class names. Two-tier:
    ///  1. position-keyed (x,y) — fastest match; updated each scan as units
    ///     are seen alive at a tile.
    ///  2. stats-keyed (maxHp, level, team) — fallback that survives moves
    ///     and deaths. When a unit moves between scans (or dies and the
    ///     position-keyed entry was never set at the new tile), the stats
    ///     key still matches the same unit. Multiple same-class enemies
    ///     collide on this key, but they're the same class anyway so the
    ///     cache returns the correct job name.
    ///
    /// Once a unit's class is identified via fingerprint lookup, the name
    /// persists across scans even when the fingerprint search fails — most
    /// notably for dead units (HP=0 patterns differ from the alive search).
    /// </summary>
    public class UnitNameCache
    {
        private readonly Dictionary<(int x, int y), string> _byPos = new();
        private readonly Dictionary<(int maxHp, int level, int team), string> _byStats = new();

        public void Set(int x, int y, string name)
        {
            _byPos[(x, y)] = name;
        }

        /// <summary>
        /// Set both position AND stats keys. Call this from the fingerprint-
        /// match path so subsequent scans can recover the name even after
        /// the unit moves or dies.
        /// </summary>
        public void Set(int x, int y, int maxHp, int level, int team, string name)
        {
            _byPos[(x, y)] = name;
            if (maxHp > 0 && level > 0)
                _byStats[(maxHp, level, team)] = name;
        }

        public string? Get(int x, int y)
        {
            return _byPos.TryGetValue((x, y), out var name) ? name : null;
        }

        /// <summary>
        /// Fallback lookup by (maxHp, level, team) when the position key
        /// missed. Returns null when no prior fingerprint match has cached
        /// a name for this stats triple.
        /// </summary>
        public string? GetByStats(int maxHp, int level, int team)
        {
            if (maxHp <= 0 || level <= 0) return null;
            return _byStats.TryGetValue((maxHp, level, team), out var name) ? name : null;
        }

        public void Move(int fromX, int fromY, int toX, int toY)
        {
            if (_byPos.TryGetValue((fromX, fromY), out var name))
            {
                _byPos.Remove((fromX, fromY));
                _byPos[(toX, toY)] = name;
            }
        }

        public void Clear()
        {
            _byPos.Clear();
            _byStats.Clear();
        }
    }
}
