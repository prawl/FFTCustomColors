using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class FacingDirectionFromMovementTests
    {
        [Theory]
        [InlineData(3, 4, 3, 5, "North")]   // moved +Y
        [InlineData(3, 4, 3, 3, "South")]   // moved -Y
        [InlineData(3, 4, 4, 4, "East")]    // moved +X
        [InlineData(3, 4, 2, 4, "West")]    // moved -X
        [InlineData(3, 4, 4, 5, "North")]   // diagonal: +X +Y, dominant Y
        [InlineData(3, 4, 5, 5, "East")]    // diagonal: +2X +1Y, dominant X
        public void DeriveFacingFromMovement_ReturnsCorrectDirection(
            int prevX, int prevY, int curX, int curY, string expectedFacing)
        {
            var result = FacingStrategy.DeriveFacingFromMovement(prevX, prevY, curX, curY);
            Assert.Equal(expectedFacing, result);
        }

        [Fact]
        public void DeriveFacingFromMovement_NoMovement_ReturnsNull()
        {
            var result = FacingStrategy.DeriveFacingFromMovement(3, 4, 3, 4);
            Assert.Null(result);
        }
    }
}
