using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Server-side ability list compaction: hides enemy-target abilities with no
    /// enemies in range and collapses numbered families (e.g. Aim +1..+20) with
    /// identical target tiles into a single entry.
    /// </summary>
    public static class AbilityCompactor
    {
        private static readonly Regex NumberedFamily = new(@"^(.+?)\s*\+?(\d+)$");

        public static List<AbilityEntry> Compact(List<AbilityEntry> abilities)
        {
            var result = new List<AbilityEntry>();

            for (int i = 0; i < abilities.Count; i++)
            {
                var a = abilities[i];

                // Hide enemy-target abilities with no enemies in range
                if (IsHidden(a))
                    continue;

                // Try to collapse numbered family (Aim +1, +2, ... +20)
                var m = NumberedFamily.Match(a.Name);
                if (m.Success)
                {
                    string prefix = m.Groups[1].Value.TrimEnd();
                    var nums = new List<string> { m.Groups[2].Value };
                    string tileKey = TileKey(a);

                    int j = i + 1;
                    while (j < abilities.Count)
                    {
                        var b = abilities[j];
                        var bm = NumberedFamily.Match(b.Name);
                        if (!bm.Success || bm.Groups[1].Value.TrimEnd() != prefix)
                            break;

                        // Skip hidden entries (enemy-target, no enemies)
                        if (IsHidden(b))
                        {
                            j++;
                            continue;
                        }

                        if (TileKey(b) != tileKey)
                            break;

                        nums.Add(bm.Groups[2].Value);
                        j++;
                    }

                    if (nums.Count > 1)
                    {
                        var collapsed = new AbilityEntry
                        {
                            Name = $"{prefix} (+{nums[0]} to +{nums[^1]})",
                            Target = a.Target,
                            HRange = a.HRange,
                            ValidTargetTiles = a.ValidTargetTiles,
                            TotalTargets = a.TotalTargets,
                            Element = a.Element,
                            AddedEffect = a.AddedEffect,
                            CastSpeed = a.CastSpeed,
                            Mp = a.Mp,
                        };
                        result.Add(collapsed);
                        i = j - 1;
                        continue;
                    }
                }

                result.Add(a);
            }

            return result;
        }

        /// <summary>
        /// An enemy-target ability with no enemy occupants in its valid-tile
        /// list contributes no useful information to the ability list — the
        /// player can't use it to hit anything. Hide it.
        /// </summary>
        public static bool IsHidden(AbilityEntry a)
        {
            return a.Target == "enemy" && !HasEnemyOccupant(a);
        }

        private static bool HasEnemyOccupant(AbilityEntry a)
        {
            return a.ValidTargetTiles?.Any(t => t.Occupant == "enemy") == true;
        }

        private static string TileKey(AbilityEntry a)
        {
            if (a.ValidTargetTiles == null || a.ValidTargetTiles.Count == 0)
                return "";
            return string.Join(";", a.ValidTargetTiles
                .Select(t => $"{t.X},{t.Y}")
                .OrderBy(s => s));
        }
    }
}
