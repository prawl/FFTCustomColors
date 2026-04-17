using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure planning logic for multi-step navigation flows. Extracted from
    /// <c>NavigationActions.NavigateToCharacterStatus</c> so the key sequence
    /// can be computed and tested in isolation, and so bridge callers can
    /// request a dry-run (plan without firing) before committing to a live
    /// run that crashed the game twice in prior sessions.
    ///
    /// Each key press is represented by its VK code + settle delay. The
    /// planner doesn't have access to a game window — it just emits the plan.
    /// </summary>
    public static class NavigationPlanner
    {
        public const int VK_UP = 0x26;
        public const int VK_RIGHT = 0x27;
        public const int VK_DOWN = 0x28;
        public const int VK_LEFT = 0x25;
        public const int VK_ESCAPE = 0x1B;
        public const int VK_ENTER = 0x0D;

        /// <summary>
        /// A single planned key press with its settle time and rationale.
        /// <paramref name="EarlyExitOnScreen"/> is an optional hint for the
        /// live executor: after the SettleMs sleep, if DetectScreen reports
        /// this screen name, SKIP the remaining steps in the current
        /// contiguous group (see <see cref="GroupId"/>). Used for the
        /// escape-storm where we issue up to N escapes but early-exit on
        /// WorldMap. Null means "always execute this step".
        /// </summary>
        public record KeyStep(
            int VkCode,
            int SettleMs,
            string Reason,
            string? EarlyExitOnScreen = null,
            string? GroupId = null);

        public record NavigateToCharacterStatusPlan(
            bool Ok,
            string? Error,
            List<KeyStep> Steps)
        {
            public int TotalMs
            {
                get
                {
                    int total = 0;
                    foreach (var s in Steps) total += s.SettleMs;
                    return total;
                }
            }

            /// <summary>
            /// Human-readable rendering of the plan for dry-run logs.
            /// Format: "ESC(1000ms, reason) + DOWN(100ms, reason) + ..."
            /// </summary>
            public string Render()
            {
                if (!Ok) return $"FAILED: {Error}";
                if (Steps.Count == 0) return "(no-op)";
                var parts = new List<string>();
                foreach (var s in Steps) parts.Add($"{VkName(s.VkCode)}({s.SettleMs}ms, {s.Reason})");
                return string.Join(" + ", parts) + $" [total {TotalMs}ms]";
            }

            private static string VkName(int vk) => vk switch
            {
                VK_UP => "UP",
                VK_DOWN => "DOWN",
                VK_LEFT => "LEFT",
                VK_RIGHT => "RIGHT",
                VK_ESCAPE => "ESC",
                VK_ENTER => "ENTER",
                _ => $"VK_{vk:X2}",
            };
        }

        /// <summary>
        /// Build the key-press plan for navigating from the current screen
        /// to a specific unit's CharacterStatus. Ports the live logic from
        /// <c>NavigationActions.NavigateToCharacterStatus</c> without the
        /// memory reads or Input.SendKey calls. Inputs:
        ///   - <paramref name="currentScreenName"/>: result of DetectScreen,
        ///     one of "WorldMap" | "PartyMenuUnits" | anything-else-in-tree.
        ///   - <paramref name="targetDisplayOrder"/>: 0-indexed grid position
        ///     for the target unit (from roster +0x122).
        ///   - <paramref name="rosterCount"/>: number of units in the roster
        ///     (total, not displayed-only).
        /// </summary>
        public static NavigateToCharacterStatusPlan PlanNavigateToCharacterStatus(
            string currentScreenName,
            int targetDisplayOrder,
            int rosterCount)
        {
            var steps = new List<KeyStep>();

            if (rosterCount <= 0)
                return new NavigateToCharacterStatusPlan(false, "rosterCount must be > 0", steps);
            if (targetDisplayOrder < 0 || targetDisplayOrder >= rosterCount)
                return new NavigateToCharacterStatusPlan(
                    false,
                    $"targetDisplayOrder {targetDisplayOrder} out of range [0, {rosterCount})",
                    steps);

            int targetRow = targetDisplayOrder / 5;
            int targetCol = targetDisplayOrder % 5;
            int gridRows = (rosterCount + 4) / 5;

            bool needWrap;
            switch (currentScreenName)
            {
                case "PartyMenuUnits":
                    // Cursor position unknown — reset to (0, 0).
                    needWrap = true;
                    break;
                case "WorldMap":
                    // Escape from WorldMap opens PartyMenu with cursor at (0, 0).
                    // 1000ms settle — shorter drops Down keys during the open
                    // animation (verified 2026-04-15 session 23).
                    steps.Add(new KeyStep(VK_ESCAPE, 1000, "WorldMap → PartyMenu"));
                    needWrap = false;
                    break;
                default:
                    // Deep in tree — storm Escapes to WorldMap, then one more
                    // to open PartyMenu. Upper bound 8; live executor early-
                    // exits the storm group when DetectScreen reports
                    // "WorldMap" after any step (via EarlyExitOnScreen hint),
                    // then fires the final "open PartyMenu" Escape which is
                    // in a different group so it doesn't get skipped.
                    //
                    // Settle time: 500ms per escape. 300ms was fast enough
                    // for the raw key press but not enough for detection to
                    // stabilize — the SM-driven TravelList→WorldMap override
                    // would fire mid-transition (SM predicted WorldMap via
                    // key-count while game was still rendering PartyMenu
                    // exit), causing the 2-read confirm to agree falsely
                    // and the final open-PartyMenu Escape to hit WorldMap
                    // again instead of its intended PartyMenu. Manual
                    // stepping with ~500ms between keys works reliably.
                    for (int i = 0; i < 8; i++)
                        steps.Add(new KeyStep(
                            VK_ESCAPE, 500,
                            $"deep-tree escape {i+1}/8",
                            EarlyExitOnScreen: "WorldMap",
                            GroupId: "escape-storm"));
                    steps.Add(new KeyStep(VK_ESCAPE, 700, "open PartyMenu from WorldMap"));
                    needWrap = false;
                    break;
            }

            // Wrap to (0, 0). gridRows Ups (up-wraps within column) +
            // 5 Lefts (left-wraps within row). Overshoot is safe because
            // edges are hard walls at (0, *) and (*, 0).
            if (needWrap)
            {
                for (int i = 0; i < gridRows; i++)
                    steps.Add(new KeyStep(VK_UP, 200, $"wrap UP {i+1}/{gridRows}"));
                for (int i = 0; i < 5; i++)
                    steps.Add(new KeyStep(VK_LEFT, 200, $"wrap LEFT {i+1}/5"));
            }

            // Navigate from (0, 0) to (targetRow, targetCol). No wrap needed.
            for (int i = 0; i < targetRow; i++)
                steps.Add(new KeyStep(VK_DOWN, 200, $"to row {i+1}/{targetRow}"));
            for (int i = 0; i < targetCol; i++)
                steps.Add(new KeyStep(VK_RIGHT, 200, $"to col {i+1}/{targetCol}"));

            // Confirm to open CharacterStatus.
            steps.Add(new KeyStep(VK_ENTER, 300, "open CharacterStatus"));

            return new NavigateToCharacterStatusPlan(true, null, steps);
        }
    }
}
