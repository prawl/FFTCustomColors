using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure MVP picker for battle-end stats. Score formula was inline inside
    /// <see cref="BattleStatTracker.EndBattle"/>; session 47 extracted it here
    /// so the scoring gets dedicated test coverage + a public shape for
    /// callers that want to display or preview the MVP during a battle.
    ///
    /// Formula:
    ///   score = kills * 300
    ///         + damageDealt
    ///         + healingDealt / 2
    ///         - timesKOd * 200
    /// </summary>
    public static class MvpSelector
    {
        public static int Score(UnitBattleStats u) =>
            u.Kills * 300
            + u.DamageDealt
            + (int)(u.HealingDealt * 0.5)
            - u.TimesKOd * 200;

        /// <summary>
        /// Pick the MVP name from a dictionary of per-unit battle stats.
        /// Returns null on empty input. Ties broken by dictionary iteration
        /// order (first-inserted wins).
        /// </summary>
        public static string? Select(Dictionary<string, UnitBattleStats> units)
        {
            if (units == null || units.Count == 0) return null;

            string? mvp = null;
            int best = int.MinValue;
            foreach (var (name, stats) in units)
            {
                int s = Score(stats);
                if (s > best)
                {
                    best = s;
                    mvp = name;
                }
            }
            return mvp;
        }
    }
}
