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
            string activePlayerName,
            IReadOnlyList<UnitScanDiff.UnitSnap>? postSnaps = null)
        {
            var lines = new List<string>();
            if (events == null || events.Count == 0) return lines;

            // The counter-attacker is whichever PLAYER took damage this window
            // (they trigger the counter via a reaction ability). Prefer the
            // caller's activePlayerName hint when that player is present in
            // the damaged list; otherwise pick the first damaged player.
            // The hint alone is NOT trusted — in chunked mode it can end up
            // reflecting the currently-acting ENEMY unit (the scan's
            // IsActive flag shifts as the turn passes to enemies).
            string? counterActor = null;
            foreach (var e in events)
            {
                if (e.Team != "PLAYER") continue;
                if (e.Kind != "damaged") continue;
                if (!e.OldHp.HasValue || !e.NewHp.HasValue) continue;
                if (e.NewHp.Value >= e.OldHp.Value) continue;
                if (!string.IsNullOrEmpty(activePlayerName) && e.Label == activePlayerName)
                {
                    counterActor = e.Label;
                    break;
                }
                counterActor ??= e.Label;
            }
            if (counterActor == null) return lines;

            // Emit a counter line for each enemy that took HP damage or was KO'd
            // in this window. We pair every enemy-HP-drop with the counter
            // actor since there's no finer-grained attribution mechanism yet.
            foreach (var e in events)
            {
                if (e.Team != "ENEMY") continue;
                if (e.Kind != "damaged" && e.Kind != "ko") continue;
                if (!e.OldHp.HasValue || !e.NewHp.HasValue) continue;
                int delta = e.OldHp.Value - e.NewHp.Value;
                if (delta <= 0) continue;

                // Sanity check — a counter delta physically can't exceed
                // the target's MaxHp (one hit maxes at MaxHp damage). If
                // postSnaps is provided and the target's MaxHp is known,
                // reject implausible deltas as animation-transient reads
                // rather than emit a wildly-wrong counter line. Live-
                // observed Knight-died-for-521 false KO when Defending
                // buff dropped mid-window.
                if (postSnaps != null)
                {
                    int? maxHp = null;
                    foreach (var s in postSnaps)
                    {
                        if (s.Name == e.Label) { maxHp = s.MaxHp; break; }
                    }
                    if (maxHp.HasValue && delta > maxHp.Value) continue;
                }

                if (e.Kind == "ko")
                    lines.Add($"> {counterActor} countered {e.Label} for {delta} dmg — {e.Label} died");
                else
                    lines.Add($"> {counterActor} countered {e.Label} for {delta} dmg");
            }
            return lines;
        }
    }
}
