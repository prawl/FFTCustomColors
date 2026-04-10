using FFTColorCustomizer.GameBridge;
using System.Collections.Generic;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class GameTileListTests
    {
        [Fact]
        public void ParseMoveTiles_ValidData_ReturnsTiles()
        {
            // 7 bytes per tile: X, Y, elev_lo, elev_hi, unknown, unknown, flag
            // flag=1 means valid, flag=0 terminates
            var bytes = new byte[]
            {
                6, 9, 10, 0, 0, 0, 1,   // tile (6,9)
                7, 9, 3, 0, 0, 0, 1,    // tile (7,9)
                5, 9, 0, 0, 0, 0, 1,    // tile (5,9)
                0, 0, 0, 0, 0, 0, 0,    // terminator
            };

            var tiles = GameTileList.Parse(bytes);

            Assert.Equal(3, tiles.Count);
            Assert.Contains((6, 9), tiles);
            Assert.Contains((7, 9), tiles);
            Assert.Contains((5, 9), tiles);
        }

        [Fact]
        public void ParseMoveTiles_EmptyList_ReturnsEmpty()
        {
            var bytes = new byte[] { 0, 0, 0, 0, 0, 0, 0 };
            var tiles = GameTileList.Parse(bytes);
            Assert.Empty(tiles);
        }

        [Fact]
        public void ParseMoveTiles_NullBytes_ReturnsEmpty()
        {
            var tiles = GameTileList.Parse(null!);
            Assert.Empty(tiles);
        }
    }
}
