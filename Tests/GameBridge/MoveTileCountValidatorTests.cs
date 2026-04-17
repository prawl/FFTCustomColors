using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class MoveTileCountValidatorTests
    {
        [Fact]
        public void Compare_WhenCountsMatch_ReturnsNull()
        {
            // BFS found 11 tiles, game reports 11 — no discrepancy.
            var result = MoveTileCountValidator.Compare(bfsCount: 11, gameCount: 11);
            Assert.Null(result);
        }

        [Fact]
        public void Compare_WhenBfsOvercounts_ReturnsLoudWarning()
        {
            // BFS found 21 tiles, game reports 11 — BFS overcounting (common case, e.g. Kenrick Siedge Weald).
            var result = MoveTileCountValidator.Compare(bfsCount: 21, gameCount: 11);
            Assert.NotNull(result);
            Assert.Contains("BFS", result);
            Assert.Contains("21", result);
            Assert.Contains("11", result);
        }

        [Fact]
        public void Compare_WhenBfsUndercounts_ReturnsLoudWarning()
        {
            var result = MoveTileCountValidator.Compare(bfsCount: 5, gameCount: 11);
            Assert.NotNull(result);
            Assert.Contains("BFS", result);
            Assert.Contains("5", result);
            Assert.Contains("11", result);
        }

        [Fact]
        public void Compare_WhenGameCountUnknown_ReturnsNull()
        {
            // Memory read failed (null) — skip comparison silently.
            var result = MoveTileCountValidator.Compare(bfsCount: 21, gameCount: null);
            Assert.Null(result);
        }

        [Fact]
        public void Compare_WhenGameCountIsZero_ReturnsNull()
        {
            // Zero is ambiguous: could be "no tiles" or "memory uninitialized". Skip to avoid false positives.
            var result = MoveTileCountValidator.Compare(bfsCount: 21, gameCount: 0);
            Assert.Null(result);
        }
    }
}
