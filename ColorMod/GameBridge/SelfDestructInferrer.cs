using System.Collections.Generic;
using System.Linq;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// S60 enemy-turn narrator Phase 2: detect bomb self-destruct patterns.
    ///
    /// Pattern: in a single event window, one enemy dies (ko) AND two or
    /// more other units take measurable HP damage. The splash profile is
    /// the canonical Bomb/Grenade self-destruct — the enemy kills itself
    /// while dealing AoE damage to nearby units.
    ///
    /// Emits "> {Bomb} self-destructed (dealt N to X, M to Y)" per qualifying
    /// dying enemy. Splash targets don't have to be players — the inferrer
    /// is team-agnostic (bombs hit their own allies just as happily).
    /// </summary>
    public static class SelfDestructInferrer
    {
        private const int MinSplashTargets = 2;

        public static List<string> Infer(IReadOnlyList<UnitScanDiff.ChangeEvent> events)
        {
            var lines = new List<string>();
            if (events == null || events.Count == 0) return lines;

            // Collect splash targets: any unit (not the dying bomb itself) that
            // took positive HP damage this window.
            var splashTargets = new List<(string label, int damage)>();
            foreach (var e in events)
            {
                if (e.Kind != "damaged") continue;
                if (!e.OldHp.HasValue || !e.NewHp.HasValue) continue;
                int delta = e.OldHp.Value - e.NewHp.Value;
                if (delta <= 0) continue;
                splashTargets.Add((e.Label, delta));
            }

            if (splashTargets.Count < MinSplashTargets) return lines;

            // For every dying enemy in this window, attribute the splash to it.
            // If multiple bombs died, each is treated as a separate self-destruct
            // — we can't disambiguate which targets belonged to which without
            // proximity data, so the same splash list is reported per-bomb.
            foreach (var e in events)
            {
                if (e.Team != "ENEMY") continue;
                if (e.Kind != "ko") continue;

                var parts = splashTargets.Select(t => $"{t.damage} to {t.label}");
                lines.Add($"> {e.Label} self-destructed (dealt {string.Join(", ", parts)})");
            }

            return lines;
        }
    }
}
