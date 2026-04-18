using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Per-weapon-type base equippability table. Source: FFTHandsFree/Wiki/weapons.txt
    /// (authoritative per-type job lists from Final Fantasy Wiki). Equip-Ability support
    /// skills (Equip Axes, Equip Swords, Equip Crossbows, Equip Guns, Equip Katana,
    /// Equip Polearms) are NOT included here — those are cross-class overrides the caller
    /// layers on top. This table only answers "does the unit's current JOB inherently
    /// allow this weapon type?"
    ///
    /// Job names are normalized to their canonical in-game forms (e.g. "Gallant Knight"
    /// for Ramza, "Onion Knight", "Dark Knight"). Case-insensitive lookup.
    /// </summary>
    public static class WeaponEquippability
    {
        private static readonly Dictionary<string, HashSet<string>> _table =
            new(System.StringComparer.OrdinalIgnoreCase)
        {
            ["Axe"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Squire", "Geomancer", "Onion Knight"
            },
            ["Bag"] = new(System.StringComparer.OrdinalIgnoreCase) {
                // Enhanced Mode of TIC: bags are exclusive to dragonkin/bard/dancer.
                "Dragonkin", "Bard", "Dancer"
            },
            ["Book"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Arithmetician", "Mystic", "Onion Knight"
            },
            ["Bow"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Archer", "Onion Knight", "Sky Pirate"
            },
            ["Cloth"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Dancer", "Onion Knight"
            },
            ["Crossbow"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Archer", "Onion Knight", "Sky Pirate", "Divine Knight"
            },
            ["Fell Sword"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Dark Knight", "Onion Knight"
            },
            ["Flail"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Squire", "Ninja", "Onion Knight", "Game Hunter"
            },
            ["Gun"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Chemist", "Orator", "Onion Knight", "Machinist", "Sky Pirate"
            },
            ["Instrument"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Bard", "Onion Knight"
            },
            ["Katana"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Samurai", "Onion Knight", "Sword Saint"
            },
            ["Knife"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Squire", "Chemist", "Thief", "Orator", "Ninja", "Dancer",
                "Onion Knight", "Game Hunter", "Sky Pirate"
            },
            ["Knight's Sword"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Gallant Knight", "Knight", "Dark Knight", "Onion Knight",
                "Holy Knight", "Game Hunter", "Sky Pirate", "Sword Saint",
                "Templar", "Divine Knight"
            },
            ["Ninja Blade"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Ninja", "Onion Knight", "Sword Saint"
            },
            ["Pole"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Arithmetician", "Mystic", "Onion Knight", "Skyseer", "Netherseer"
            },
            ["Rod"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Black Mage", "Summoner", "Mystic", "Netherseer", "Onion Knight"
            },
            ["Polearm"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Dragoon", "Onion Knight", "Sky Pirate", "Divine Knight"
            },
            ["Staff"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "White Mage", "Time Mage", "Summoner", "Mystic", "Skyseer", "Onion Knight"
            },
            ["Sword"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Squire", "Knight", "Geomancer", "Dark Knight", "Onion Knight",
                "Holy Knight", "Game Hunter", "Sky Pirate", "Sword Saint",
                "Divine Knight", "Templar", "Soldier"
            },
        };

        /// <summary>
        /// True if the given job can inherently equip the given weapon type (without
        /// an Equip-* support ability).
        /// </summary>
        public static bool CanJobEquip(string job, string weaponType)
        {
            if (string.IsNullOrWhiteSpace(job) || string.IsNullOrWhiteSpace(weaponType))
                return false;
            return _table.TryGetValue(weaponType.Trim(), out var jobs)
                && jobs.Contains(job.Trim());
        }

        /// <summary>
        /// All jobs that inherently equip this weapon type. Empty set if the type is
        /// unknown.
        /// </summary>
        public static IReadOnlyCollection<string> GetJobsFor(string weaponType)
        {
            if (string.IsNullOrWhiteSpace(weaponType)) return System.Array.Empty<string>();
            return _table.TryGetValue(weaponType.Trim(), out var jobs)
                ? jobs
                : System.Array.Empty<string>();
        }

        /// <summary>
        /// All known weapon types in the table.
        /// </summary>
        public static IReadOnlyCollection<string> AllWeaponTypes => _table.Keys;
    }
}
