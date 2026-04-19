using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Centralized UX aliases for ValidPath action names. Users reflexively
    /// type familiar synonyms — "Leave" on shop-adjacent screens, "Back" on
    /// menu-tree screens, "Yes" / "No" on confirm modals. This helper
    /// applies canonical aliasing to a paths dictionary: when one name in
    /// an alias group is present and another is missing, the missing name
    /// is added as an alias pointing at the same <see cref="PathEntry"/>.
    ///
    /// Session 47: Extracted from inline logic in <see cref="NavigationPaths.GetPaths"/>.
    /// Keeps the post-processor testable, makes new alias groups a
    /// one-line addition, and removes the risk of drift between the
    /// aliasing rules and their tests.
    ///
    /// Semantics:
    ///   - Alias groups are sets of names that mean the same thing.
    ///   - If ANY name from the group is defined in the paths dict, every
    ///     other name from the group is added as an alias (same entry).
    ///   - Names already defined are preserved — the helper never overwrites.
    /// </summary>
    public static class ActionNameAliases
    {
        /// <summary>
        /// Canonical alias groups. Adding a new group:
        ///   1. Add it here with all synonyms a user might type.
        ///   2. Add a test to NavigationPathsAliasTests that pins one
        ///      known screen + asserts every name reaches the same entry.
        /// </summary>
        public static readonly string[][] Groups = new[]
        {
            // Exit verbs — Leave (shop-like) ↔ Back (menu-tree) ↔ Exit (common
            // natural-language synonym). All three mean "close this screen,
            // return to the previous one."
            new[] { "Leave", "Back", "Exit" },
            // Confirm-modal affirmatives — OK / Yes / Confirm. Propagates only
            // on screens that already define one of these (confirm dialogs,
            // crystal prompts). Picker screens use Select (different semantic)
            // and are unaffected because they don't define any of these names.
            new[] { "Confirm", "OK", "Yes" },
        };

        /// <summary>
        /// Apply alias groups to the given paths dict in-place. Returns the
        /// same dict for chaining convenience. Null is tolerated and
        /// returned as-is.
        /// </summary>
        public static Dictionary<string, PathEntry>? ApplyAliases(
            Dictionary<string, PathEntry>? paths)
        {
            if (paths == null) return null;

            foreach (var group in Groups)
            {
                // Find which name (if any) from the group exists. First
                // match wins — alias groups are assumed to be semantically
                // equivalent, so it doesn't matter which entry we pick.
                PathEntry? canonical = null;
                foreach (var name in group)
                {
                    if (paths.TryGetValue(name, out var entry))
                    {
                        canonical = entry;
                        break;
                    }
                }

                if (canonical == null) continue;

                // Propagate to every name in the group that isn't already set.
                foreach (var name in group)
                {
                    if (!paths.ContainsKey(name))
                        paths[name] = canonical;
                }
            }

            return paths;
        }
    }
}
