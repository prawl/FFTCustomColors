using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pin the (liveMove, liveJump, jobName) → (effectiveMove, effectiveJump)
    /// resolution contract. Live values take precedence; table values fill
    /// in when heap search returns 0; unknown jobs collapse to 0.
    /// </summary>
    public class MoveJumpFallbackResolverTests
    {
        [Fact]
        public void LiveValues_Present_AreUsedVerbatim()
        {
            var (m, j) = MoveJumpFallbackResolver.Resolve(liveMove: 7, liveJump: 4, jobName: "Gallant Knight");
            Assert.Equal((7, 4), (m, j));
        }

        [Fact]
        public void LiveValuesZero_KnownJob_FillsFromTable()
        {
            var (m, j) = MoveJumpFallbackResolver.Resolve(liveMove: 0, liveJump: 0, jobName: "Knight");
            Assert.Equal((3, 3), (m, j));
        }

        [Fact]
        public void LiveMoveZero_LiveJumpPresent_MixesTableMoveWithLiveJump()
        {
            var (m, j) = MoveJumpFallbackResolver.Resolve(liveMove: 0, liveJump: 5, jobName: "Thief");
            Assert.Equal((4, 5), (m, j));
        }

        [Fact]
        public void LiveJumpZero_LiveMovePresent_MixesLiveMoveWithTableJump()
        {
            var (m, j) = MoveJumpFallbackResolver.Resolve(liveMove: 6, liveJump: 0, jobName: "Monk");
            Assert.Equal((6, 4), (m, j));
        }

        [Fact]
        public void LiveValuesZero_UnknownJob_ReturnsZero()
        {
            var (m, j) = MoveJumpFallbackResolver.Resolve(liveMove: 0, liveJump: 0, jobName: "Nonexistent");
            Assert.Equal((0, 0), (m, j));
        }

        [Fact]
        public void LiveValuesZero_NullJob_ReturnsZero()
        {
            var (m, j) = MoveJumpFallbackResolver.Resolve(liveMove: 0, liveJump: 0, jobName: null);
            Assert.Equal((0, 0), (m, j));
        }

        [Fact]
        public void NegativeLiveValues_TreatedAsMissing()
        {
            var (m, j) = MoveJumpFallbackResolver.Resolve(liveMove: -1, liveJump: -1, jobName: "Archer");
            Assert.Equal((3, 3), (m, j));
        }
    }
}
