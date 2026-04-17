using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Per FFHacktics canonical rules, a tile's edge height depends on the
    /// slope type + which edge you're looking at. Flat tiles have edge height
    /// = Height on all four sides. Incline tiles have two edges at Height
    /// (low) and two edges at Height + SlopeHeight (high). Convex/Concave are
    /// corner slopes — two adjacent sides high/low in an L shape.
    ///
    /// Direction convention: "N" = -Y, "S" = +Y, "E" = +X, "W" = -X.
    /// </summary>
    public class TileEdgeHeightTests
    {
        [Fact]
        public void Flat_AllEdgesEqualHeight()
        {
            var t = new MapTile { Height = 5, SlopeHeight = 0, SlopeType = "Flat 0" };
            Assert.Equal(5, TileEdgeHeight.Edge(t, Direction.N));
            Assert.Equal(5, TileEdgeHeight.Edge(t, Direction.S));
            Assert.Equal(5, TileEdgeHeight.Edge(t, Direction.E));
            Assert.Equal(5, TileEdgeHeight.Edge(t, Direction.W));
        }

        [Fact]
        public void InclineN_HighOnNorth_LowOnSouth()
        {
            // Incline N: rises toward the north. N edge = top, S edge = bottom.
            var t = new MapTile { Height = 2, SlopeHeight = 1, SlopeType = "Incline N" };
            Assert.Equal(3, TileEdgeHeight.Edge(t, Direction.N));
            Assert.Equal(2, TileEdgeHeight.Edge(t, Direction.S));
            // E/W edges span the slope; treat as the average (2.5).
            Assert.Equal(2.5, TileEdgeHeight.Edge(t, Direction.E));
            Assert.Equal(2.5, TileEdgeHeight.Edge(t, Direction.W));
        }

        [Fact]
        public void InclineS_HighOnSouth_LowOnNorth()
        {
            var t = new MapTile { Height = 4, SlopeHeight = 2, SlopeType = "Incline S" };
            Assert.Equal(4, TileEdgeHeight.Edge(t, Direction.N));
            Assert.Equal(6, TileEdgeHeight.Edge(t, Direction.S));
        }

        [Fact]
        public void InclineE_HighOnEast_LowOnWest()
        {
            var t = new MapTile { Height = 1, SlopeHeight = 3, SlopeType = "Incline E" };
            Assert.Equal(4, TileEdgeHeight.Edge(t, Direction.E));
            Assert.Equal(1, TileEdgeHeight.Edge(t, Direction.W));
        }

        [Fact]
        public void InclineW_HighOnWest_LowOnEast()
        {
            var t = new MapTile { Height = 1, SlopeHeight = 3, SlopeType = "Incline W" };
            Assert.Equal(4, TileEdgeHeight.Edge(t, Direction.W));
            Assert.Equal(1, TileEdgeHeight.Edge(t, Direction.E));
        }

        [Fact]
        public void ConvexNE_HighOnNorthAndEast()
        {
            // Convex NE: the NE corner is raised. N and E edges are high.
            var t = new MapTile { Height = 4, SlopeHeight = 1, SlopeType = "Convex NE" };
            Assert.Equal(5, TileEdgeHeight.Edge(t, Direction.N));
            Assert.Equal(5, TileEdgeHeight.Edge(t, Direction.E));
            Assert.Equal(4, TileEdgeHeight.Edge(t, Direction.S));
            Assert.Equal(4, TileEdgeHeight.Edge(t, Direction.W));
        }

        [Fact]
        public void ConvexSW_HighOnSouthAndWest()
        {
            var t = new MapTile { Height = 3, SlopeHeight = 2, SlopeType = "Convex SW" };
            Assert.Equal(5, TileEdgeHeight.Edge(t, Direction.S));
            Assert.Equal(5, TileEdgeHeight.Edge(t, Direction.W));
            Assert.Equal(3, TileEdgeHeight.Edge(t, Direction.N));
            Assert.Equal(3, TileEdgeHeight.Edge(t, Direction.E));
        }

        [Fact]
        public void ConcaveNE_LowOnNorthAndEast()
        {
            // Concave NE: the NE corner is depressed. N and E edges are low,
            // S and W edges are high.
            var t = new MapTile { Height = 3, SlopeHeight = 1, SlopeType = "Concave NE" };
            Assert.Equal(3, TileEdgeHeight.Edge(t, Direction.N));
            Assert.Equal(3, TileEdgeHeight.Edge(t, Direction.E));
            Assert.Equal(4, TileEdgeHeight.Edge(t, Direction.S));
            Assert.Equal(4, TileEdgeHeight.Edge(t, Direction.W));
        }

        [Fact]
        public void UnknownSlopeType_FallsBackToFlatDisplayHeight()
        {
            var t = new MapTile { Height = 5, SlopeHeight = 2, SlopeType = "Unknown Thing" };
            // Fallback matches legacy GetDisplayHeight (Height + SlopeHeight/2).
            Assert.Equal(6.0, TileEdgeHeight.Edge(t, Direction.N));
            Assert.Equal(6.0, TileEdgeHeight.Edge(t, Direction.S));
        }
    }
}
