using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure renderer for enemy-turn narration appended to `battle_wait` response.Info.
    ///
    /// Takes a <see cref="UnitScanDiff.ChangeEvent"/> list produced by comparing
    /// pre-wait and post-wait unit snapshots, plus the active player's name.
    /// Returns a list of `"> ..."` lines suitable for multi-line info rendering.
    ///
    /// Session 60. See plan: enemy-turn narrator Phase 1.
    /// </summary>
    public static class BattleNarratorRenderer
    {
        private const int MaxLines = 8;

        public static List<string> Render(
            IReadOnlyList<UnitScanDiff.ChangeEvent> events,
            string activePlayerName,
            HashSet<string>? suppressedKoLabels = null)
        {
            var lines = new List<string>();
            if (events == null || events.Count == 0) return lines;

            int skipped = 0;
            foreach (var e in events)
            {
                if (lines.Count >= MaxLines) { skipped++; continue; }

                // S60 Phase 2.5: when a higher-level inferrer (counter/self-
                // destruct) already emits a line that mentions the death, skip
                // the raw "> X died" so the narration doesn't duplicate itself.
                if (e.Kind == "ko"
                    && suppressedKoLabels != null
                    && suppressedKoLabels.Contains(e.Label))
                {
                    continue;
                }

                var formatted = FormatEvent(e);

                // status kind produces ONLY gained/lost lines (no main line).
                if (e.Kind == "status")
                {
                    bool hasGained = e.StatusesGained != null && e.StatusesGained.Count > 0;
                    bool hasLost = e.StatusesLost != null && e.StatusesLost.Count > 0;
                    if (hasGained && lines.Count < MaxLines)
                        lines.Add($"> {e.Label} gained {string.Join(", ", e.StatusesGained!)}");
                    if (hasLost && lines.Count < MaxLines)
                        lines.Add($"> {e.Label} lost {string.Join(", ", e.StatusesLost!)}");
                    continue;
                }

                if (formatted == null) continue;
                lines.Add(formatted);

                // S60 fix: status changes that accompany damaged/healed/ko/revived
                // events must also be surfaced — UnitScanDiff.Compare collapses them
                // into the main Kind, so "Skeletal Fiend took 336 damage" otherwise
                // silently hides the accompanying "gained Petrify" from a Chaos
                // Blade on-hit proc. Render as a trailing status line.
                if (e.StatusesGained != null && e.StatusesGained.Count > 0
                    && lines.Count < MaxLines)
                {
                    lines.Add($"> {e.Label} gained {string.Join(", ", e.StatusesGained)}");
                }
                if (e.StatusesLost != null && e.StatusesLost.Count > 0
                    && lines.Count < MaxLines)
                {
                    lines.Add($"> {e.Label} lost {string.Join(", ", e.StatusesLost)}");
                }
            }

            if (skipped > 0)
                lines.Add($"> ... (+{skipped} more)");

            return lines;
        }

        /// <summary>
        /// Format a single event to its "> ..." line. Returns null for events
        /// that should be skipped entirely (added/removed, move without coords).
        /// For status events, returns a placeholder string and the caller
        /// handles gained/lost line emission separately.
        /// </summary>
        private static string? FormatEvent(UnitScanDiff.ChangeEvent e)
        {
            switch (e.Kind)
            {
                case "moved":
                    if (!e.OldXY.HasValue || !e.NewXY.HasValue) return null;
                    return $"> {e.Label} moved ({e.OldXY.Value.x},{e.OldXY.Value.y}) → ({e.NewXY.Value.x},{e.NewXY.Value.y})";

                case "damaged":
                    if (e.OldHp.HasValue && e.NewHp.HasValue)
                    {
                        int dmg = e.OldHp.Value - e.NewHp.Value;
                        return $"> {e.Label} took {dmg} damage (HP {e.OldHp.Value}→{e.NewHp.Value})";
                    }
                    return $"> {e.Label} took damage";

                case "healed":
                    if (e.OldHp.HasValue && e.NewHp.HasValue)
                    {
                        int heal = e.NewHp.Value - e.OldHp.Value;
                        return $"> {e.Label} recovered {heal} HP (HP {e.OldHp.Value}→{e.NewHp.Value})";
                    }
                    return $"> {e.Label} recovered HP";

                case "ko":
                    return $"> {e.Label} died";

                case "revived":
                    if (e.OldHp.HasValue && e.NewHp.HasValue)
                        return $"> {e.Label} revived (HP {e.OldHp.Value}→{e.NewHp.Value})";
                    return $"> {e.Label} revived";

                case "status":
                    // Signal to caller to emit gained/lost lines itself.
                    return "";

                case "added":
                    // A unit appeared on the field that wasn't here pre-wait —
                    // most commonly a story-scripted guest joining mid-battle
                    // (Boco, Tietra, Agrias, Mustadio, etc), or a summon/clone
                    // spawned by an enemy. Surface so the player isn't blind-
                    // sided. Live-flagged playtest #4 2026-04-25: Tietra
                    // appeared without ceremony at the climax of Siedge Weald.
                    if (e.NewXY.HasValue)
                        return $"> {e.Label} ({e.Team}) joined at ({e.NewXY.Value.x},{e.NewXY.Value.y})";
                    return $"> {e.Label} ({e.Team}) joined the battle";

                case "removed":
                    // 2026-04-26: a unit going from alive (OldHp > 0) to
                    // missing from the post-snap is a death-equivalent the
                    // narrator should surface. Live-flagged playtest #3:
                    // Skeleton 344/680 → DEAD with no kill-feed entry,
                    // agent couldn't tell what happened. PhantomKoCoalescer
                    // suppresses the false-positive "removed + added"
                    // pairs from transient bad scans, so what remains here
                    // is real.
                    if (e.OldHp.HasValue && e.OldHp.Value > 0)
                        return $"> {e.Label} died";
                    return null;
                case "noop":
                default:
                    return null;
            }
        }
    }
}
