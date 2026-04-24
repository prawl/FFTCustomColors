using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// S60 enemy-turn narrator Phase 2: best-effort inference of counter
    /// attacks from a battle_wait event window.
    ///
    /// Pattern: within a single window, the active player took damage AND
    /// at least one enemy's HP dropped. Without an active-unit-index byte
    /// we can't confirm who struck whom, so we attribute the enemy's HP
    /// drop to the player's counter-attack. If the enemy died, include
    /// " — {Enemy} died". Renders ready-to-append "> ..." lines.
    ///
    /// Never suppresses raw events — the caller decides how to combine
    /// inferred lines with BattleNarratorRenderer output.
    /// </summary>
    public static class CounterAttackInferrer
    {
        public static List<string> Infer(
            IReadOnlyList<UnitScanDiff.ChangeEvent> events,
            string activePlayerName)
        {
            var lines = new List<string>();
            if (events == null || events.Count == 0) return lines;
            if (string.IsNullOrEmpty(activePlayerName)) return lines;

            // Look for the active player's damaged event.
            bool playerDamaged = false;
            foreach (var e in events)
            {
                if (e.Label == activePlayerName
                    && e.Kind == "damaged"
                    && e.OldHp.HasValue
                    && e.NewHp.HasValue
                    && e.NewHp.Value < e.OldHp.Value)
                {
                    playerDamaged = true;
                    break;
                }
            }
            if (!playerDamaged) return lines;

            // Emit a counter line for each enemy that took HP damage or was KO'd
            // in this window. We pair every enemy-HP-drop with the player's
            // single damaged event since there's no attribution mechanism yet.
            foreach (var e in events)
            {
                if (e.Team != "ENEMY") continue;
                if (e.Kind != "damaged" && e.Kind != "ko") continue;
                if (!e.OldHp.HasValue || !e.NewHp.HasValue) continue;
                int delta = e.OldHp.Value - e.NewHp.Value;
                if (delta <= 0) continue;

                if (e.Kind == "ko")
                    lines.Add($"> {activePlayerName} countered {e.Label} for {delta} dmg — {e.Label} died");
                else
                    lines.Add($"> {activePlayerName} countered {e.Label} for {delta} dmg");
            }
            return lines;
        }
    }
}
