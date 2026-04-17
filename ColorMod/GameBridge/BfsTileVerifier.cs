using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Diagnostic: compare our BFS-computed valid-move tiles against the
    /// game's own valid-tile set (observed via cursor-flag at +0x06 =
    /// 0x05 for "cursor on valid tile"). Used during Move mode to find
    /// maps/units where our BFS is wrong so we can fix the algorithm.
    /// Not runtime — diagnostic only.
    /// </summary>
    public static class BfsTileVerifier
    {
        public record VerifyResult(
            List<(int x, int y)> Agreements,
            List<(int x, int y)> FalsePositives,
            List<(int x, int y)> FalseNegatives);

        public static VerifyResult Compare(
            IReadOnlyList<(int x, int y)> bfsTiles,
            IReadOnlyList<(int x, int y)> gameTiles)
        {
            var bfsSet = new HashSet<(int, int)>(bfsTiles);
            var gameSet = new HashSet<(int, int)>(gameTiles);

            var agreements = bfsSet.Intersect(gameSet).OrderBy(t => t.Item2).ThenBy(t => t.Item1).ToList();
            var falsePositives = bfsSet.Except(gameSet).OrderBy(t => t.Item2).ThenBy(t => t.Item1).ToList();
            var falseNegatives = gameSet.Except(bfsSet).OrderBy(t => t.Item2).ThenBy(t => t.Item1).ToList();

            return new VerifyResult(agreements, falsePositives, falseNegatives);
        }

        public static string FormatReport(VerifyResult result)
        {
            var sb = new StringBuilder();
            sb.Append($"[BFS VERIFY] {result.Agreements.Count} agree, ");
            sb.Append($"{result.FalsePositives.Count} false positive(s), ");
            sb.Append($"{result.FalseNegatives.Count} false negative(s).");
            if (result.FalsePositives.Count > 0)
            {
                sb.Append(" FP (BFS said valid, game rejected): ");
                sb.Append(string.Join(", ", result.FalsePositives.Select(t => $"({t.x},{t.y})")));
                sb.Append('.');
            }
            if (result.FalseNegatives.Count > 0)
            {
                sb.Append(" FN (game valid, BFS missed): ");
                sb.Append(string.Join(", ", result.FalseNegatives.Select(t => $"({t.x},{t.y})")));
                sb.Append('.');
            }
            return sb.ToString();
        }
    }
}
