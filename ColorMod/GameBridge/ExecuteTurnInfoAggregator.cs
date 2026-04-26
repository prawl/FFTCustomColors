using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure helper for combining per-step Info strings + an optional
    /// turn-interrupt message into a single response.Info string for
    /// execute_turn bundles. Two problems this solves:
    ///
    /// 1) Inline `string.Join(" | ", stepInfos)` in CommandWatcher
    ///    overwrote the [turn-interrupt] message that was set on
    ///    last.Info during the abort branch — the message was lost
    ///    on every GameOver / BattleVictory terminal exit.
    ///
    /// 2) fft.sh's narrator filter only surfaces lines starting with
    ///    "&gt; ", "===", or "[OUTCOME". Per-step Info from battle_move /
    ///    battle_ability (e.g. "(9,8)-&gt;(9,7) CONFIRMED") was stored
    ///    in response.Info but never reached the user's terminal.
    ///
    /// Aggregator prefixes each step's Info with "&gt; [action]" so the
    /// shell narrator picks each entry up as its own line. Already-
    /// prefixed entries (recap "[OUTCOME ...]", banner "=== ===",
    /// pre-formatted "&gt; ..." narrator lines) pass through unchanged.
    /// </summary>
    public static class ExecuteTurnInfoAggregator
    {
        public static string? Aggregate(
            IEnumerable<(string Action, string? Info)> steps,
            string? turnInterruptMessage = null)
        {
            var lines = new List<string>();
            if (steps != null)
            {
                foreach (var (action, info) in steps)
                {
                    if (string.IsNullOrWhiteSpace(info)) continue;
                    lines.Add(FormatLine(action, info!));
                }
            }
            if (!string.IsNullOrWhiteSpace(turnInterruptMessage))
                lines.Add(EnsureNarratorPrefix(turnInterruptMessage!));
            return lines.Count == 0 ? null : string.Join(" | ", lines);
        }

        private static string FormatLine(string action, string info)
        {
            if (IsAlreadyNarratorReady(info)) return info;
            return $"> [{action}] {info}";
        }

        private static string EnsureNarratorPrefix(string s)
            => IsAlreadyNarratorReady(s) ? s : $"> {s}";

        private static bool IsAlreadyNarratorReady(string s)
            => s.StartsWith("> ")
               || s.StartsWith("===")
               || s.StartsWith("[OUTCOME");
    }
}
