using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Caches unit class names by grid position. Once a unit's class is identified
    /// via fingerprint lookup, the name persists even when the fingerprint search
    /// fails (e.g. after HP changes alter the search pattern).
    /// </summary>
    public class UnitNameCache
    {
        private readonly Dictionary<(int x, int y), string> _cache = new();

        public void Set(int x, int y, string name)
        {
            _cache[(x, y)] = name;
        }

        public string? Get(int x, int y)
        {
            return _cache.TryGetValue((x, y), out var name) ? name : null;
        }

        public void Move(int fromX, int fromY, int toX, int toY)
        {
            if (_cache.TryGetValue((fromX, fromY), out var name))
            {
                _cache.Remove((fromX, fromY));
                _cache[(toX, toY)] = name;
            }
        }

        public void Clear()
        {
            _cache.Clear();
        }
    }
}
