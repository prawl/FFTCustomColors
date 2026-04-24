using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Reverse lookup: live-battle map-id byte (at
    /// <see cref="LiveBattleMapId.Address"/>) → world-map location id.
    ///
    /// Inverse of <c>random_encounter_maps.json</c>, held in-memory so
    /// curLoc= rendering doesn't need a file read per scan. Covers the
    /// 19 random-encounter maps (locIds 24..42). Story-battle maps are
    /// not in this table — callers fall back to <c>_lastWorldMapLocation</c>.
    ///
    /// Motivation: when the player walks from one battleground to another
    /// via an intermediate location, <c>_lastWorldMapLocation</c> latches on
    /// the wrong name. The live map byte is authoritative — use it first
    /// whenever we're inside a battle.
    /// </summary>
    public static class BattleMapIdToLocation
    {
        private static readonly Dictionary<int, int> _mapIdToLocId = new()
        {
            { 71, 36 }, // Dorvauldar Marsh
            { 72, 25 }, // Fovoham Windflats
            { 74, 26 }, // The Siedge Weald
            { 75, 27 }, // Mount Bervenia
            { 76, 28 }, // Zeklaus Desert
            { 77, 29 }, // Lenalian Plateau
            { 78, 30 }, // Tchigolith Fenlands
            { 79, 31 }, // The Yuguewood
            { 80, 32 }, // Araguay Woods
            { 81, 33 }, // Grogh Heights
            { 82, 34 }, // Beddha Sandwaste
            { 83, 35 }, // Zeirchele Falls
            { 84, 37 }, // Balias Tor
            { 85, 24 }, // Mandalia Plain
            { 86, 38 }, // Dugeura Pass
            { 87, 39 }, // Balias Swale
            { 88, 40 }, // Finnath Creek
            { 89, 41 }, // Lake Poescas
            { 90, 42 }, // Mount Germinas
        };

        /// <summary>
        /// Returns the locId for a given battle map-id, or -1 if not in
        /// the random-encounter table.
        /// </summary>
        public static int TryResolve(int mapId)
        {
            return _mapIdToLocId.TryGetValue(mapId, out var locId) ? locId : -1;
        }
    }
}
