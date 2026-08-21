using System.Collections.Generic;
using FFTColorCustomizer.Services;

namespace FFTColorCustomizer.ThemeEditor
{
    /// <summary>
    /// One row in the theme editor's Monsters dropdown group: a single (family, rank) pair.
    /// <see cref="DisplayName"/> is the rank's real name plus a 1-based "(rank N)" suffix (e.g.
    /// "Black Chocobo (rank 2)"). <see cref="Family"/> is the tier-agnostic
    /// <see cref="MonsterFamily.EditorKey"/> -- the key used for section-mapping lookup,
    /// sprite-path resolution AND the saved-theme key, so it must stay the family name, never a
    /// per-rank name, or existing user themes would break. <see cref="PaletteIndex"/> is the bin
    /// palette (0/1/2) that rank's colours live at. See CC-27.
    /// </summary>
    public sealed record MonsterEditorEntry(string DisplayName, string Family, int PaletteIndex);

    /// <summary>
    /// Turns <see cref="MonsterThemeRegistry.Families"/> into the theme editor's per rank
    /// dropdown entries -- one row per (family, rank) instead of one row per family, so ranks II
    /// and III are reachable instead of silently aliasing to rank I's colours. A per-family
    /// divider row was tried and dropped again (it cluttered the dropdown); the existing
    /// "-- Monsters --" group header above the whole section already marks it as a group. Pure
    /// and filesystem-free: the caller supplies which families actually have a section mapping
    /// on disk (Data/SectionMappings/Monster/&lt;Family&gt;.json) so a family missing one is
    /// skipped rather than producing a dropdown row with nothing to load. See CC-27.
    /// </summary>
    public static class MonsterEditorEntries
    {
        /// <param name="availableMonsters">Family names with a section mapping on disk.</param>
        /// <param name="families">
        /// Registry to draw from -- defaults to <see cref="MonsterThemeRegistry.Families"/>. Only
        /// a test should ever pass something else; it exists so a family's PaletteIndices link
        /// (not just its default values) is directly provable without needing to alter the real
        /// registry. See CC-27.
        /// </param>
        public static IReadOnlyList<MonsterEditorEntry> Build(
            IEnumerable<string> availableMonsters, IEnumerable<MonsterFamily>? families = null)
        {
            var available = new HashSet<string>(availableMonsters);
            var entries = new List<MonsterEditorEntry>();

            foreach (var family in families ?? MonsterThemeRegistry.Families)
            {
                if (!available.Contains(family.Family))
                    continue;

                for (int tier = 0; tier < family.TierDisplayNames.Length; tier++)
                {
                    entries.Add(new MonsterEditorEntry(
                        $"{family.TierDisplayNames[tier]} (rank {tier + 1})",
                        family.Family, family.PaletteIndices[tier]));
                }
            }

            return entries;
        }
    }
}
