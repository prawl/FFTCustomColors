using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class BfsTileVerifierTests
    {
        [Fact]
        public void Compare_WhenBfsAndGameAgreeExactly_NoDiscrepancies()
        {
            var bfs = new List<(int x, int y)> { (1, 1), (2, 1), (1, 2) };
            var game = new List<(int x, int y)> { (1, 1), (2, 1), (1, 2) };
            var result = BfsTileVerifier.Compare(bfs, game);
            Assert.Empty(result.FalsePositives);
            Assert.Empty(result.FalseNegatives);
            Assert.Equal(3, result.Agreements.Count);
        }

        [Fact]
        public void Compare_WhenBfsHasExtraTile_FlagsItFalsePositive()
        {
            // BFS over-counts (common case — e.g. steep-cliff tile)
            var bfs = new List<(int x, int y)> { (1, 1), (2, 1), (10, 6) };
            var game = new List<(int x, int y)> { (1, 1), (2, 1) };
            var result = BfsTileVerifier.Compare(bfs, game);
            Assert.Single(result.FalsePositives);
            Assert.Contains((10, 6), result.FalsePositives);
            Assert.Empty(result.FalseNegatives);
            Assert.Equal(2, result.Agreements.Count);
        }

        [Fact]
        public void Compare_WhenBfsMissesTile_FlagsItFalseNegative()
        {
            // BFS under-counts (rare but possible — ally-traversal edge case)
            var bfs = new List<(int x, int y)> { (1, 1) };
            var game = new List<(int x, int y)> { (1, 1), (2, 1), (3, 1) };
            var result = BfsTileVerifier.Compare(bfs, game);
            Assert.Empty(result.FalsePositives);
            Assert.Equal(2, result.FalseNegatives.Count);
            Assert.Contains((2, 1), result.FalseNegatives);
            Assert.Contains((3, 1), result.FalseNegatives);
            Assert.Single(result.Agreements);
        }

        [Fact]
        public void Compare_MixedCase_BothDirections()
        {
            var bfs = new List<(int x, int y)> { (1, 1), (2, 2), (10, 6) };
            var game = new List<(int x, int y)> { (1, 1), (2, 2), (4, 4) };
            var result = BfsTileVerifier.Compare(bfs, game);
            Assert.Single(result.FalsePositives);
            Assert.Contains((10, 6), result.FalsePositives);
            Assert.Single(result.FalseNegatives);
            Assert.Contains((4, 4), result.FalseNegatives);
            Assert.Equal(2, result.Agreements.Count);
        }

        [Fact]
        public void Compare_EmptyInputs_ReturnsEmptyResult()
        {
            var result = BfsTileVerifier.Compare(
                new List<(int x, int y)>(),
                new List<(int x, int y)>());
            Assert.Empty(result.FalsePositives);
            Assert.Empty(result.FalseNegatives);
            Assert.Empty(result.Agreements);
        }

        [Fact]
        public void FormatReport_RendersHumanReadableSummary()
        {
            var result = new BfsTileVerifier.VerifyResult(
                Agreements: new List<(int, int)> { (1, 1), (2, 2) },
                FalsePositives: new List<(int, int)> { (10, 6) },
                FalseNegatives: new List<(int, int)> { (4, 4) });
            var report = BfsTileVerifier.FormatReport(result);
            Assert.Contains("2 agree", report);
            Assert.Contains("1 false positive", report);
            Assert.Contains("1 false negative", report);
            Assert.Contains("(10,6)", report);
            Assert.Contains("(4,4)", report);
        }
    }
}
