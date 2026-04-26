using System.Collections.Generic;
using System.Linq;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Compress a UnitScanDiff event list into a single-line recap of
    /// action effects (HP delta, status, KO) suitable for the head of an
    /// `execute_turn` response. Movement events are filtered out — the
    /// narrator's `> X moved` lines surface those separately, and they
    /// dilute the action signal in the recap.
    /// Live-flagged 2026-04-25 playtest: agent could not tell from
    /// `execute_turn` response whether their Hasteja landed / their
    /// Phoenix Down revived the target.
    /// </summary>
    public static class OutcomeRecapRenderer
    {
        public static string? Render(IReadOnlyList<UnitScanDiff.ChangeEvent>? events)
        {
            if (events == null || events.Count == 0) return null;
            // Split by team so the agent can tell their own action effects
            // (player units) apart from ambient changes (enemy self-buffs
            // during animation, enemies acting on enemies, etc). Live-
            // flagged 2026-04-26 P3: agent saw `[OUTCOME] Archer +163 HP
            // +Defending -Charging,Haste PA +15,MA -9` after attacking
            // Knight and didn't realize that was the enemy's Steel buff,
            // not their attack.
            var allyParts = new List<string>();
            var enemyParts = new List<string>();
            foreach (var e in events)
            {
                var part = RenderOne(e);
                if (part == null) continue;
                if (e.Team == "PLAYER" || e.Team == "ALLY")
                    allyParts.Add(part);
                else
                    enemyParts.Add(part);
            }
            if (allyParts.Count == 0 && enemyParts.Count == 0) return null;
            var sections = new List<string>();
            if (allyParts.Count > 0)
                sections.Add("[OUTCOME yours] " + string.Join(" / ", allyParts));
            if (enemyParts.Count > 0)
                sections.Add("[OUTCOME enemies] " + string.Join(" / ", enemyParts));
            return string.Join(" | ", sections);
        }

        private static string? RenderOne(UnitScanDiff.ChangeEvent e)
        {
            // Filter movement-only events — those go into the narrator's
            // `> X moved` lines, not the action recap.
            if (e.Kind == "moved" || e.Kind == "noop") return null;

            if (e.Kind == "ko") return $"{e.Label} KO'd";
            if (e.Kind == "revived") return $"{e.Label} REVIVED";

            string? hpPart = null;
            if (e.OldHp.HasValue && e.NewHp.HasValue && e.OldHp.Value != e.NewHp.Value)
            {
                int delta = e.NewHp.Value - e.OldHp.Value;
                string sign = delta >= 0 ? "+" : "";
                hpPart = $"{sign}{delta} HP";
            }

            string? statusGainPart = null;
            if (e.StatusesGained != null && e.StatusesGained.Count > 0)
                statusGainPart = "+" + string.Join(",", e.StatusesGained);

            string? statusLossPart = null;
            if (e.StatusesLost != null && e.StatusesLost.Count > 0)
                statusLossPart = "-" + string.Join(",", e.StatusesLost);

            // Stat deltas (Speed Surge +1, Tailwind PA +2, etc) — render
            // as `Speed +1, PA +2` so the recap reads naturally alongside
            // status/HP changes.
            string? statPart = null;
            if (e.StatDeltas != null && e.StatDeltas.Count > 0)
                statPart = string.Join(",", e.StatDeltas);

            var pieces = new[] { hpPart, statusGainPart, statusLossPart, statPart }
                .Where(p => p != null).ToList();
            if (pieces.Count == 0) return null;
            return $"{e.Label} {string.Join(" ", pieces)}";
        }
    }
}
