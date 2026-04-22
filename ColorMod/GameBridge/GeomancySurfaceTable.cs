using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure lookup table mapping map-tile surface types to the Geomancer's
    /// Elemental ability for that surface. Geomancy's "pick ability by
    /// terrain" mechanic is entirely deterministic — the surface byte on
    /// the tile the caster occupies selects which of ~10 abilities fires.
    ///
    /// Surface IDs sourced from FFHacktics wiki map-tile-type table (PSX
    /// canonical). Flip entries here if IC remaster divergence is
    /// live-captured in a session.
    /// </summary>
    public static class GeomancySurfaceTable
    {
        private static readonly Dictionary<int, (string Surface, string Ability)> _map = new()
        {
            [0] = ("Natural Surface", "Local Quake"),
            [1] = ("Stone Wall", "Local Quake"),
            [2] = ("Wasteland", "Pitfall"),
            [3] = ("Swamp", "Water Ball"),
            [4] = ("Grassland", "Hell Ivy"),
            [5] = ("Bushes", "Sand Storm"),
            [6] = ("Tree", "Sand Storm"),
            [7] = ("Snow", "Blizzard"),
            [8] = ("Rocky Cliff", "Gusty Wind"),
            [9] = ("Gravel", "Lava Ball"),
            [10] = ("River", "Will-o-the-Wisp"),
            [11] = ("Lake", "Will-o-the-Wisp"),
            [12] = ("Sea", "Will-o-the-Wisp"),
            [13] = ("Bridge", "Sand Storm"),
            [14] = ("Ice", "Blizzard"),
        };

        public static IReadOnlyCollection<int> KnownSurfaceIds => _map.Keys;

        public static string? AbilityFor(int surfaceId)
        {
            return _map.TryGetValue(surfaceId, out var entry) ? entry.Ability : null;
        }

        public static string SurfaceName(int surfaceId)
        {
            return _map.TryGetValue(surfaceId, out var entry) ? entry.Surface : "Unknown";
        }
    }
}
