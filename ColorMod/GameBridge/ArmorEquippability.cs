using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Per-armor-type base equippability table. Source: FFTHandsFree/Wiki/armor.txt.
    /// Equip-* support skills (Equip Heavy Armor, Equip Shields) are NOT included —
    /// caller layers those on top. This table only answers "does the unit's current
    /// JOB inherently allow this armor type?"
    ///
    /// Clothes and Hats are expressed as EXCLUSION lists per the Wiki phrasing
    /// ("all job classes except for ..."). The public API normalizes both.
    /// </summary>
    public static class ArmorEquippability
    {
        private static readonly Dictionary<string, HashSet<string>> _inclusive =
            new(System.StringComparer.OrdinalIgnoreCase)
        {
            // Armor (heavy) — castle-bought; physical-class list.
            ["Armor"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Squire", "Knight", "Dragoon", "Samurai", "Dark Knight",
                "Onion Knight", "Game Hunter", "Sky Pirate", "Holy Knight",
                "Sword Saint", "Divine Knight", "Templar"
            },
            // Helmet (heavy) — same core list as Armor, minus some late-game templars.
            ["Helmet"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Squire", "Knight", "Dragoon", "Samurai", "Dark Knight",
                "Onion Knight", "Game Hunter", "Sky Pirate", "Sword Saint",
                "Divine Knight"
            },
            // Robe — the broad caster/hybrid list.
            ["Robe"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Squire", "Knight", "White Mage", "Black Mage", "Time Mage",
                "Summoner", "Orator", "Mystic", "Geomancer", "Dragoon", "Samurai",
                "Arithmetician", "Dark Knight", "Onion Knight", "Game Hunter",
                "Sky Pirate", "Skyseer", "Netherseer", "Sword Saint", "Divine Knight"
            },
            // Shield — physical + Archer + Geomancer hybrids.
            ["Shield"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Squire", "Gallant Knight", "Knight", "Archer", "Geomancer",
                "Dragoon", "Dark Knight", "Onion Knight", "Game Hunter",
                "Sky Pirate", "Sword Saint", "Divine Knight"
            },
        };

        // Exclusion lists: "all jobs EXCEPT these".
        private static readonly Dictionary<string, HashSet<string>> _exclusive =
            new(System.StringComparer.OrdinalIgnoreCase)
        {
            ["Clothes"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Knight", "Gallant Knight", "Holy Knight", "Dark Knight",
                "Divine Knight", "Templar", "Sword Saint",
                "Dragoon", "Samurai", "Mime", "Dragonkin"
            },
            ["Hat"] = new(System.StringComparer.OrdinalIgnoreCase) {
                "Knight", "Monk", "Dragoon", "Samurai", "Mime",
                "Dark Knight", "Divine Knight"
            },
        };

        /// <summary>
        /// True if the given job can inherently equip the given armor type.
        /// </summary>
        public static bool CanJobEquip(string job, string armorType)
        {
            if (string.IsNullOrWhiteSpace(job) || string.IsNullOrWhiteSpace(armorType))
                return false;
            var j = job.Trim();
            var t = armorType.Trim();
            if (_inclusive.TryGetValue(t, out var included))
                return included.Contains(j);
            if (_exclusive.TryGetValue(t, out var excluded))
                return !excluded.Contains(j);
            return false;
        }

        public static IReadOnlyCollection<string> AllArmorTypes
        {
            get
            {
                var list = new List<string>(_inclusive.Keys);
                list.AddRange(_exclusive.Keys);
                return list;
            }
        }
    }
}
