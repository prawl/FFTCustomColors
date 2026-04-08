using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Maps roster nameIds to character names for story characters.
    /// Generic units (recruited at Warriors' Guild) have high nameIds (200+)
    /// that aren't in this table — they return null.
    /// </summary>
    public static class UnitNameLookup
    {
        private static readonly Dictionary<int, string> StoryCharacters = new()
        {
            { 1, "Ramza" },
            { 13, "Orlandeau" },
            { 15, "Reis" },
            { 22, "Mustadio" },
            { 26, "Marach" },
            { 30, "Agrias" },
            { 31, "Beowulf" },
            { 41, "Rapha" },
            { 42, "Meliadoul" },
            { 50, "Cloud" },
            { 117, "Construct 8" },
            // WotL-exclusive
            { 59, "Balthier" },
            { 60, "Luso" },
            // Temporary guests (appear in battle but may not be permanent)
            { 5, "Delita" },
            { 9, "Argath" },
            { 16, "Gaffgarion" },
            { 38, "Alma" },
            { 37, "Ovelia" },
            { 40, "Orran" },
        };

        /// <summary>
        /// Get character name by roster nameId. Returns null for generic/unknown units.
        /// </summary>
        public static string? GetName(int nameId)
        {
            if (nameId <= 0) return null;
            return StoryCharacters.TryGetValue(nameId, out var name) ? name : null;
        }
    }
}
