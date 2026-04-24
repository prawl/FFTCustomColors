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
            string activePlayerName)
        {
            var lines = new List<string>();
            if (events == null || events.Count == 0) return lines;

            int skipped = 0;
            foreach (var e in events)
            {
                if (lines.Count >= MaxLines) { skipped++; continue; }

                var formatted = FormatEvent(e);
                if (formatted == null) continue;

                // status kind can produce TWO lines (gained + lost); handle first
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

                lines.Add(formatted);
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
                case "removed":
                case "noop":
                default:
                    return null;
            }
        }
    }
}
