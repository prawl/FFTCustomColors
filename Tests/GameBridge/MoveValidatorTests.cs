using FFTColorCustomizer.GameBridge;
using System.Collections.Generic;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class MoveValidatorTests
    {
        [Fact]
        public void ValidateTile_InValidSet_ReturnsTrue()
        {
            var validTiles = new HashSet<(int, int)> { (6, 9), (7, 9), (8, 9) };
            Assert.True(MoveValidator.IsValidTile(7, 9, validTiles));
        }

        [Fact]
        public void ValidateTile_NotInValidSet_ReturnsFalse()
        {
            var validTiles = new HashSet<(int, int)> { (6, 9), (7, 9), (8, 9) };
            Assert.False(MoveValidator.IsValidTile(4, 9, validTiles));
        }

        [Fact]
        public void ValidateTile_NullValidTiles_ReturnsTrue()
        {
            // If no valid tiles cached (scan hasn't run), allow the move
            // and let the game reject it if invalid
            Assert.True(MoveValidator.IsValidTile(4, 9, null));
        }

        [Fact]
        public void ValidateTile_EmptyValidTiles_ReturnsFalse()
        {
            var validTiles = new HashSet<(int, int)>();
            Assert.False(MoveValidator.IsValidTile(4, 9, validTiles));
        }

        [Fact]
        public void GetErrorMessage_InvalidTile_IncludesCoordinates()
        {
            var validTiles = new HashSet<(int, int)> { (6, 9), (7, 9) };
            var error = MoveValidator.GetInvalidTileError(4, 9, validTiles);
            Assert.Contains("(4,9)", error);
            Assert.Contains("2 valid tiles", error);
        }
    }
}
