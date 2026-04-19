using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Helpers that wrap <see cref="NavigationPaths.GetPaths"/> to expose
    /// human-readable descriptions of the actions available on a screen.
    /// Consumer: the fail-loud error returned by
    /// <c>ExecuteValidPath</c> when a user types an unknown action.
    ///
    /// Session 47: extracted so the error message can list not just
    /// action names but their Desc field, making the fail-loud signal
    /// actually useful when a typo occurs. Pure logic — any caller
    /// can use these to render a help block without touching
    /// NavigationPaths directly.
    /// </summary>
    public static class NavigationPathsDescription
    {
        /// <summary>
        /// Returns the Desc string for a single action on a named screen,
        /// or null if either the screen or the action isn't defined.
        /// </summary>
        public static string? GetPathDescription(string screenName, string actionName)
        {
            if (string.IsNullOrWhiteSpace(screenName) || string.IsNullOrWhiteSpace(actionName))
                return null;
            var paths = NavigationPaths.GetPaths(
                new Utilities.DetectedScreen { Name = screenName });
            if (paths == null) return null;
            return paths.TryGetValue(actionName, out var entry) ? entry.Desc : null;
        }

        /// <summary>
        /// Formats a comma-separated list of action names on the screen
        /// (aliases coalesced with "/" e.g. "Leave/Back/Exit"). Cheaper
        /// to compute + shorter than <see cref="FormatAvailableActions"/>;
        /// use when the caller wants just the names (e.g. quick suggestion
        /// in a terse log line, a shell completion hint).
        /// </summary>
        public static string FormatActionNames(string? screenName)
        {
            if (string.IsNullOrWhiteSpace(screenName)) return "none";
            var paths = NavigationPaths.GetPaths(
                new Utilities.DetectedScreen { Name = screenName });
            if (paths == null || paths.Count == 0) return "none";

            var seen = new HashSet<PathEntry>();
            var groups = new List<List<string>>();
            foreach (var (name, entry) in paths)
            {
                if (seen.Add(entry))
                {
                    groups.Add(new List<string> { name });
                }
                else
                {
                    foreach (var g in groups)
                    {
                        if (paths[g[0]] == entry)
                        {
                            g.Add(name);
                            break;
                        }
                    }
                }
            }

            var sb = new StringBuilder();
            for (int i = 0; i < groups.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(string.Join("/", groups[i]));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Formats the "Available: Name — Desc" block used in fail-loud
        /// errors. Lists every action on the screen with its description,
        /// one per line. Returns "none" when the screen has no paths.
        /// Callers concatenate this onto the core error message.
        ///
        /// Aliases (same PathEntry reference) are coalesced so "Leave"
        /// and "Back" don't render as two separate lines saying the same
        /// thing. The first name encountered wins.
        /// </summary>
        public static string FormatAvailableActions(string? screenName)
        {
            if (string.IsNullOrWhiteSpace(screenName)) return "none";
            var paths = NavigationPaths.GetPaths(
                new Utilities.DetectedScreen { Name = screenName });
            if (paths == null || paths.Count == 0) return "none";

            // Group by PathEntry reference to coalesce aliases.
            var seen = new HashSet<PathEntry>();
            var groups = new List<(List<string> names, PathEntry entry)>();
            foreach (var (name, entry) in paths)
            {
                if (seen.Add(entry))
                {
                    groups.Add((new List<string> { name }, entry));
                }
                else
                {
                    // Find the existing group for this entry and append.
                    foreach (var g in groups)
                    {
                        if (ReferenceEquals(g.entry, entry))
                        {
                            g.names.Add(name);
                            break;
                        }
                    }
                }
            }

            var sb = new StringBuilder();
            for (int i = 0; i < groups.Count; i++)
            {
                if (i > 0) sb.Append("; ");
                var (names, entry) = groups[i];
                sb.Append(string.Join("/", names));
                if (!string.IsNullOrEmpty(entry.Desc))
                {
                    sb.Append(" — ");
                    sb.Append(entry.Desc);
                }
            }
            return sb.ToString();
        }
    }
}
