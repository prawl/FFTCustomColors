using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class AttackDirectionTests
    {
        // Given a known "Right arrow = delta" from empirical test,
        // compute the correct arrow key to reach a target tile.

        [Fact]
        public void ComputeArrowKey_RightIs0Plus1_TargetAbove_ReturnsUp()
        {
            // Right=(0,+1), want delta (-1,0) → should be Up
            var result = AttackDirectionLogic.ComputeArrowForDelta(
                rightDx: 0, rightDy: 1, targetDx: -1, targetDy: 0);
            Assert.Equal("Up", result);
        }

        [Fact]
        public void ComputeArrowKey_RightIs0Plus1_TargetRight_ReturnsRight()
        {
            // Right=(0,+1), want delta (0,+1) → should be Right
            var result = AttackDirectionLogic.ComputeArrowForDelta(
                rightDx: 0, rightDy: 1, targetDx: 0, targetDy: 1);
            Assert.Equal("Right", result);
        }

        [Fact]
        public void ComputeArrowKey_RightIs0Plus1_TargetLeft_ReturnsLeft()
        {
            // Right=(0,+1), want delta (0,-1) → should be Left
            var result = AttackDirectionLogic.ComputeArrowForDelta(
                rightDx: 0, rightDy: 1, targetDx: 0, targetDy: -1);
            Assert.Equal("Left", result);
        }

        [Fact]
        public void ComputeArrowKey_RightIs0Plus1_TargetBelow_ReturnsDown()
        {
            // Right=(0,+1), want delta (+1,0) → should be Down
            var result = AttackDirectionLogic.ComputeArrowForDelta(
                rightDx: 0, rightDy: 1, targetDx: 1, targetDy: 0);
            Assert.Equal("Down", result);
        }

        [Fact]
        public void ComputeArrowKey_RightIs1Plus0_TargetRight_ReturnsRight()
        {
            // Right=(+1,0) (rotation 1), want delta (+1,0) → Right
            var result = AttackDirectionLogic.ComputeArrowForDelta(
                rightDx: 1, rightDy: 0, targetDx: 1, targetDy: 0);
            Assert.Equal("Right", result);
        }

        [Fact]
        public void ComputeArrowKey_RightIs1Plus0_TargetYPlus1_ReturnsUp()
        {
            // Right=(+1,0), want delta (0,+1) → Up
            var result = AttackDirectionLogic.ComputeArrowForDelta(
                rightDx: 1, rightDy: 0, targetDx: 0, targetDy: 1);
            Assert.Equal("Up", result);
        }

        [Fact]
        public void ComputeArrowKey_RightIs0Minus1_TargetXPlus1_ReturnsUp()
        {
            // Right=(0,-1) (rotation 2), want delta (+1,0) → Up
            var result = AttackDirectionLogic.ComputeArrowForDelta(
                rightDx: 0, rightDy: -1, targetDx: 1, targetDy: 0);
            Assert.Equal("Up", result);
        }

        [Fact]
        public void ComputeArrowKey_InvalidDelta_ReturnsNull()
        {
            // Diagonal delta — not a cardinal direction
            var result = AttackDirectionLogic.ComputeArrowForDelta(
                rightDx: 0, rightDy: 1, targetDx: 1, targetDy: 1);
            Assert.Null(result);
        }

        [Fact]
        public void ComputeArrowKey_ZeroDelta_ReturnsNull()
        {
            var result = AttackDirectionLogic.ComputeArrowForDelta(
                rightDx: 0, rightDy: 1, targetDx: 0, targetDy: 0);
            Assert.Null(result);
        }

        // RightDeltaFromCameraRotation: derive the Right arrow delta from the raw
        // camera rotation byte at 0x14077C970. Used as fallback when empirical
        // rotation detection fails (e.g. Jump targeting where cursor doesn't move).

        [Theory]
        [InlineData(1, 0, 1)]   // raw=1 → eff=0 → Right=(0,+1)
        [InlineData(2, 1, 0)]   // raw=2 → eff=1 → Right=(+1,0)
        [InlineData(3, 0, -1)]  // raw=3 → eff=2 → Right=(0,-1)
        [InlineData(4, -1, 0)]  // raw=4 → eff=3 → Right=(-1,0)
        [InlineData(5, 0, 1)]   // raw=5 → wraps to eff=0
        [InlineData(0, -1, 0)]  // raw=0 → eff=3 (wraps)
        public void RightDeltaFromCameraRotation_ReturnsCorrectDelta(int rawRotation, int expectedDx, int expectedDy)
        {
            var (dx, dy) = AttackDirectionLogic.RightDeltaFromCameraRotation(rawRotation);
            Assert.Equal(expectedDx, dx);
            Assert.Equal(expectedDy, dy);
        }
    }
}
