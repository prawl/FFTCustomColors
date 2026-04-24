using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Canonical WotL Move / Jump per job name. Approximate threat-assessment
    /// fallback when the per-unit heap Move/Jump read misses (typical for
    /// non-active units — CollectUnitPositionsFull only searches the active
    /// unit's struct). Equipment bonuses (Sprint Shoes, Feather Boots, Move +N
    /// ability) are NOT reflected — game-accurate base only.
    ///
    /// Name keys must match the strings emitted by CharacterData.GetJobName
    /// and ClassFingerprintLookup.GetJobName, which are the only paths that
    /// populate BattleUnitState.JobName.
    /// </summary>
    public static class JobBaseStatsTable
    {
        private static readonly Dictionary<string, (int move, int jump)> _stats = new()
        {
            // Generic human jobs (Wiki/Jobs.md, authoritative WotL values)
            ["Squire"] = (4, 3),
            ["Chemist"] = (3, 3),
            ["Knight"] = (3, 3),
            ["Archer"] = (3, 3),
            ["Monk"] = (3, 4),
            ["White Mage"] = (3, 3),
            ["Black Mage"] = (3, 3),
            ["Time Mage"] = (3, 3),
            ["Summoner"] = (3, 3),
            ["Thief"] = (4, 4),
            ["Orator"] = (3, 3),
            ["Mystic"] = (3, 3),
            ["Geomancer"] = (4, 3),
            ["Dragoon"] = (3, 4),
            ["Samurai"] = (3, 3),
            ["Ninja"] = (4, 3),
            ["Arithmetician"] = (3, 3),
            ["Bard"] = (3, 3),
            ["Dancer"] = (3, 3),
            ["Mime"] = (4, 4),
            ["Onion Knight"] = (3, 3),
            ["Dark Knight"] = (3, 3),

            // Monsters (WotL canonical values — approximate threat-assessment
            // aids; not equipment-adjusted). Flying monsters get Jp=5+ to
            // reflect their ignore-elevation trait.
            ["Goblin"] = (4, 3),
            ["Black Goblin"] = (4, 3),
            ["Gobbledygook"] = (4, 3),
            ["Skeleton"] = (3, 3),
            ["Bonesnatch"] = (3, 3),
            ["Skeletal Fiend"] = (3, 3),
            ["Ghoul"] = (3, 3),
            ["Ghast"] = (3, 3),
            ["Revenant"] = (3, 3),
            ["Chocobo"] = (4, 4),
            ["Black Chocobo"] = (4, 6),
            ["Red Chocobo"] = (4, 4),
            ["Bomb"] = (4, 3),
            ["Grenade"] = (4, 3),
            ["Exploder"] = (4, 3),
            ["Floating Eye"] = (3, 6),
            ["Ahriman"] = (3, 6),
            ["Plague Horror"] = (3, 6),
            ["Red Panther"] = (4, 4),
            ["Coeurl"] = (4, 4),
            ["Vampire Cat"] = (4, 4),
            ["Dragon"] = (3, 3),
            ["Blue Dragon"] = (3, 3),
            ["Red Dragon"] = (3, 3),
            ["Minotaur"] = (3, 3),
            ["Wisenkin"] = (3, 3),
            ["Sacred"] = (3, 3),
            ["Sekhret"] = (3, 3),
            ["Malboro"] = (3, 3),
            ["Ochu"] = (3, 3),
            ["Great Malboro"] = (3, 3),
            ["Dryad"] = (3, 3),
            ["Treant"] = (3, 3),
            ["Elder Treant"] = (3, 3),
            ["Juravis"] = (3, 5),
            ["Jura Aevis"] = (3, 5),
            ["Steelhawk"] = (3, 5),
            ["Cockatrice"] = (3, 5),
            ["Behemoth"] = (4, 3),
            ["Behemoth King"] = (4, 3),
            ["Dark Behemoth"] = (4, 3),
            ["Pig"] = (3, 3),
            ["Swine"] = (3, 3),
            ["Wild Boar"] = (3, 3),
            ["Piscodaemon"] = (3, 3),
            ["Squidraken"] = (3, 3),
            ["Mindflayer"] = (3, 3),
            ["Hydra"] = (3, 3),
            ["Greater Hydra"] = (3, 3),
            ["Tiamat"] = (3, 3),

            // Story character unique jobs
            ["Gallant Knight"] = (4, 3),
            ["Heretic"] = (4, 3),
            ["Holy Knight"] = (3, 3),
            ["Machinist"] = (3, 3),
            ["Engineer"] = (3, 3),
            ["Templar"] = (3, 3),
            ["Divine Knight"] = (3, 3),
            ["Sword Saint"] = (3, 4),
            ["Thunder God"] = (3, 4),
            ["Skyseer"] = (3, 3),
            ["Netherseer"] = (3, 3),
            ["Heaven Knight"] = (3, 3),
            ["Hell Knight"] = (3, 3),
            ["Fell Knight"] = (3, 3),
            ["Dragonkin"] = (3, 4),
            ["Holy Dragon"] = (3, 3),
            ["Soldier"] = (3, 3),
            ["Automaton"] = (3, 3),
            ["Steel Giant"] = (3, 3),
            ["Sky Pirate"] = (4, 4),
            ["Game Hunter"] = (3, 3),
            ["Princess"] = (3, 3),
            ["Cleric"] = (3, 3),
            ["Astrologer"] = (3, 3),
            ["Arc Knight"] = (3, 3),
            ["Rune Knight"] = (3, 3),
            ["Assassin"] = (4, 4),
            ["Nightblade"] = (4, 4),
            ["White Knight"] = (3, 3),
        };

        public static (int move, int jump)? TryGet(string? jobName)
        {
            if (string.IsNullOrEmpty(jobName)) return null;
            return _stats.TryGetValue(jobName!, out var v) ? v : (null as (int, int)?);
        }
    }
}
