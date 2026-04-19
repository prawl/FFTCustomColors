using System.Collections.Generic;
using System.Text;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure C# formatter for the compact one-line screen summary. Today
    /// <c>fft.sh</c> does this via a ~100-line Node + shell pipeline; this
    /// class mirrors the core header-line logic as a testable function so:
    ///   - Any C# caller can render a compact screen line without shelling out.
    ///   - The rendering rules get unit test coverage (previously the shell
    ///     rendered silently, with no way to assert expected output).
    ///   - The shell can gradually delegate to this formatter rather than
    ///     maintaining its own Node parse.
    ///
    /// Scope (session 47): ONLY the header line — `[Screen]` plus the
    /// primary decision fields (location / status / ui / objective / active
    /// unit summary). Subordinate rendering (dialogue bubbles, picker lists,
    /// battle info blocks) stays in fft.sh for now; extending this class
    /// to cover them is follow-up work.
    /// </summary>
    public static class ScreenCompactFormatter
    {
        /// <summary>
        /// Render the compact one-line header for a detected screen.
        /// Matches the `fft.sh _fmt_screen_compact` header-line shape:
        ///   "[Screen]" + battle-or-world decoration
        /// </summary>
        public static string FormatHeader(DetectedScreen? screen, string? status = null)
        {
            if (screen == null) return "[Unknown]";
            var sb = new StringBuilder();
            sb.Append('[').Append(screen.Name ?? "Unknown").Append(']');

            var isBattle = screen.Name != null && screen.Name.StartsWith("Battle");
            if (isBattle)
                AppendBattleLine(sb, screen, status);
            else
                AppendWorldSideLine(sb, screen, status);

            return sb.ToString();
        }

        private static void AppendBattleLine(StringBuilder sb, DetectedScreen screen, string? status)
        {
            // Battle screens: active unit banner, then ui=.
            if (!string.IsNullOrEmpty(screen.ActiveUnitSummary))
            {
                sb.Append(' ').Append(screen.ActiveUnitSummary);
            }
            else if (!string.IsNullOrEmpty(screen.ActiveUnitName))
            {
                sb.Append(' ').Append(screen.ActiveUnitName);
                if (!string.IsNullOrEmpty(screen.ActiveUnitJob))
                    sb.Append('(').Append(screen.ActiveUnitJob).Append(')');
            }

            if (!string.IsNullOrEmpty(screen.UI))
                sb.Append(" ui=").Append(screen.UI);

            if (!string.IsNullOrEmpty(status))
                sb.Append(" status=").Append(status);
        }

        private static void AppendWorldSideLine(StringBuilder sb, DetectedScreen screen, string? status)
        {
            // World-side screens: location + hover + ui + status + optional objective.
            if (screen.Location != 0 || !string.IsNullOrEmpty(screen.LocationName))
            {
                sb.Append(" loc=").Append(screen.Location);
                if (!string.IsNullOrEmpty(screen.LocationName))
                    sb.Append('(').Append(screen.LocationName).Append(')');
            }

            if (!string.IsNullOrEmpty(screen.UI))
                sb.Append(" ui=").Append(screen.UI);

            if (!string.IsNullOrEmpty(status))
                sb.Append(" status=").Append(status);

            if (screen.StoryObjective > 0)
            {
                sb.Append(" objective=").Append(screen.StoryObjective);
                if (!string.IsNullOrEmpty(screen.StoryObjectiveName))
                    sb.Append('(').Append(screen.StoryObjectiveName).Append(')');
            }
        }
    }
}
