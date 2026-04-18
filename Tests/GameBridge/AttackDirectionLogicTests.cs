using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class AttackDirectionLogicTests
    {
        // === RightDeltaFromCameraRotation ===
        // Camera rotation byte at 0x14077C970 is offset by 1: effective = (raw-1) mod 4.

        [Theory]
        [InlineData(1, 0, 1)]   // eff=0 → Right = (0, 1) South
        [InlineData(2, 1, 0)]   // eff=1 → Right = (1, 0) East
        [InlineData(3, 0, -1)]  // eff=2 → Right = (0, -1) North
        [InlineData(4, -1, 0)]  // eff=3 → Right = (-1, 0) West
        public void RightDeltaFromCameraRotation_ValidValues(int raw, int expectedDx, int expectedDy)
        {
            var (dx, dy) = AttackDirectionLogic.RightDeltaFromCameraRotation(raw);
            Assert.Equal(expectedDx, dx);
            Assert.Equal(expectedDy, dy);
        }

        [Fact]
        public void RightDeltaFromCameraRotation_WrapsEvery4()
        {
            // raw=5 should behave like raw=1 (both give eff=0).
            Assert.Equal(AttackDirectionLogic.RightDeltaFromCameraRotation(1),
                         AttackDirectionLogic.RightDeltaFromCameraRotation(5));
            Assert.Equal(AttackDirectionLogic.RightDeltaFromCameraRotation(2),
                         AttackDirectionLogic.RightDeltaFromCameraRotation(6));
        }

        [Fact]
        public void RightDeltaFromCameraRotation_HandlesNegative()
        {
            // raw=0 → eff=3 → Right=(-1,0)
            Assert.Equal((-1, 0), AttackDirectionLogic.RightDeltaFromCameraRotation(0));
            // raw=-3 → eff=0 → Right=(0,1)
            Assert.Equal((0, 1), AttackDirectionLogic.RightDeltaFromCameraRotation(-3));
        }

        [Fact]
        public void RightDeltaFromCameraRotation_AllFourRotations_AreDistinct()
        {
            var a = AttackDirectionLogic.RightDeltaFromCameraRotation(1);
            var b = AttackDirectionLogic.RightDeltaFromCameraRotation(2);
            var c = AttackDirectionLogic.RightDeltaFromCameraRotation(3);
            var d = AttackDirectionLogic.RightDeltaFromCameraRotation(4);
            Assert.NotEqual(a, b);
            Assert.NotEqual(a, c);
            Assert.NotEqual(a, d);
            Assert.NotEqual(b, c);
            Assert.NotEqual(b, d);
            Assert.NotEqual(c, d);
        }

        // === ComputeArrowForDelta ===

        [Fact]
        public void ComputeArrow_TargetMatchesRight_ReturnsRight()
        {
            // Right = (1, 0), target (1, 0) → Right
            Assert.Equal("Right", AttackDirectionLogic.ComputeArrowForDelta(1, 0, 1, 0));
        }

        [Fact]
        public void ComputeArrow_TargetOppositeOfRight_ReturnsLeft()
        {
            Assert.Equal("Left", AttackDirectionLogic.ComputeArrowForDelta(1, 0, -1, 0));
        }

        [Fact]
        public void ComputeArrow_90CW_ReturnsDown()
        {
            // Right = (1, 0) → Down = 90° CW = (0, -1)
            Assert.Equal("Down", AttackDirectionLogic.ComputeArrowForDelta(1, 0, 0, -1));
        }

        [Fact]
        public void ComputeArrow_90CCW_ReturnsUp()
        {
            // Right = (1, 0) → Up = 90° CCW = (0, 1)
            Assert.Equal("Up", AttackDirectionLogic.ComputeArrowForDelta(1, 0, 0, 1));
        }

        [Fact]
        public void ComputeArrow_ZeroTarget_ReturnsNull()
        {
            Assert.Null(AttackDirectionLogic.ComputeArrowForDelta(1, 0, 0, 0));
        }

        [Fact]
        public void ComputeArrow_NonCardinalTarget_ReturnsNull()
        {
            // Diagonal (1, 1) doesn't match any cardinal rotation from (1, 0).
            Assert.Null(AttackDirectionLogic.ComputeArrowForDelta(1, 0, 1, 1));
        }

        [Theory]
        [InlineData(0, 1)]   // Right pointing South
        [InlineData(1, 0)]   // Right pointing East
        [InlineData(0, -1)]  // Right pointing North
        [InlineData(-1, 0)]  // Right pointing West
        public void ComputeArrow_AllFourRotations_MatchSelfAsRight(int rdx, int rdy)
        {
            Assert.Equal("Right", AttackDirectionLogic.ComputeArrowForDelta(rdx, rdy, rdx, rdy));
        }

        [Fact]
        public void ComputeArrow_FullCycle_FromRightSouthRotation()
        {
            // Right=(0,1) South. Down = (1,0) East. Left = (0,-1) North. Up = (-1,0) West.
            Assert.Equal("Right", AttackDirectionLogic.ComputeArrowForDelta(0, 1, 0, 1));
            Assert.Equal("Down",  AttackDirectionLogic.ComputeArrowForDelta(0, 1, 1, 0));
            Assert.Equal("Left",  AttackDirectionLogic.ComputeArrowForDelta(0, 1, 0, -1));
            Assert.Equal("Up",    AttackDirectionLogic.ComputeArrowForDelta(0, 1, -1, 0));
        }

        [Fact]
        public void ComputeArrow_FullCycle_FromRightNorthRotation()
        {
            // Right=(0,-1) North. Down=(-1,0) West. Left=(0,1) South. Up=(1,0) East.
            Assert.Equal("Right", AttackDirectionLogic.ComputeArrowForDelta(0, -1, 0, -1));
            Assert.Equal("Down",  AttackDirectionLogic.ComputeArrowForDelta(0, -1, -1, 0));
            Assert.Equal("Left",  AttackDirectionLogic.ComputeArrowForDelta(0, -1, 0, 1));
            Assert.Equal("Up",    AttackDirectionLogic.ComputeArrowForDelta(0, -1, 1, 0));
        }

        [Fact]
        public void ComputeArrow_EveryCardinalTarget_GetsDistinctArrow()
        {
            // From any rotation, the 4 cardinal targets should map to 4 distinct arrows.
            var arrows = new[] {
                AttackDirectionLogic.ComputeArrowForDelta(1, 0, 1, 0),
                AttackDirectionLogic.ComputeArrowForDelta(1, 0, -1, 0),
                AttackDirectionLogic.ComputeArrowForDelta(1, 0, 0, 1),
                AttackDirectionLogic.ComputeArrowForDelta(1, 0, 0, -1),
            };
            Assert.Equal(4, System.Linq.Enumerable.Distinct(arrows).Count());
        }
    }
}
