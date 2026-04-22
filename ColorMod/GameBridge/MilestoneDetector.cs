using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure helper: detect milestones crossed between two snapshots of a
    /// unit's lifetime stats. Used after each battle to emit one-shot
    /// callouts like "Ramza reached 100 kills!" without repeating them.
    ///
    /// Milestones:
    ///   - First kill (kills: 0 → 1+)
    ///   - Kills: 10, 50, 100, 500
    ///   - Damage dealt: 1,000 / 5,000 / 10,000 / 50,000
    ///   - Battles participated: 10, 50, 100
    /// </summary>
    public static class MilestoneDetector
    {
        private static readonly int[] KillThresholds = { 10, 50, 100, 500 };
        private static readonly int[] DamageThresholds = { 1_000, 5_000, 10_000, 50_000 };
        private static readonly int[] BattleThresholds = { 10, 50, 100 };

        public static List<string> Detect(
            UnitLifetimeStats before,
            UnitLifetimeStats after)
        {
            var result = new List<string>();
            string name = after.Name;

            // First-kill callout
            if (before.TotalKills == 0 && after.TotalKills >= 1)
                result.Add($"🎯 {name} scored their first kill!");

            foreach (var t in KillThresholds)
            {
                if (before.TotalKills < t && after.TotalKills >= t)
                    result.Add($"🏆 {name} reached {t} kills!");
            }

            foreach (var t in DamageThresholds)
            {
                if (before.TotalDamageDealt < t && after.TotalDamageDealt >= t)
                    result.Add($"💥 {name} dealt {t:N0} total damage!");
            }

            foreach (var t in BattleThresholds)
            {
                if (before.TotalBattles < t && after.TotalBattles >= t)
                    result.Add($"⚔️ {name} fought in {t} battles!");
            }

            return result;
        }

        /// <summary>
        /// Detect milestones for every unit in the after-snapshot. Missing
        /// units in the before-snapshot are treated as fresh (all zeros) —
        /// mid-playthrough recruits still get a first-kill callout.
        /// </summary>
        public static List<string> DetectAll(LifetimeStats before, LifetimeStats after)
        {
            var result = new List<string>();
            foreach (var (name, afterStats) in after.Units)
            {
                if (!before.Units.TryGetValue(name, out var beforeStats))
                    beforeStats = new UnitLifetimeStats { Name = name };
                result.AddRange(Detect(beforeStats, afterStats));
            }
            return result;
        }
    }
}
