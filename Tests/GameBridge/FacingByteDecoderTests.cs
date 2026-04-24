using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class FacingByteDecoderTests
    {
        // Encoding verified live at Siedge Weald MAP074 across 4 player units
        // (Ramza S=0, Wilham S=0, Lloyd E=3, Kenrick W=1). North inferred from
        // 0/1/2/3 cardinal cycle completion.

        [Theory]
        [InlineData(0, "South")]
        [InlineData(1, "West")]
        [InlineData(2, "North")]
        [InlineData(3, "East")]
        public void DecodeName_ReturnsCardinalForValidBytes(int raw, string expected)
        {
            Assert.Equal(expected, FacingByteDecoder.DecodeName(raw));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(4)]
        [InlineData(0xFF)]
        [InlineData(0x80)]
        public void DecodeName_ReturnsNullForOutOfRange(int raw)
        {
            Assert.Null(FacingByteDecoder.DecodeName(raw));
        }

        [Theory]
        [InlineData(0, 0, 1)]   // South
        [InlineData(1, -1, 0)]  // West
        [InlineData(2, 0, -1)]  // North
        [InlineData(3, 1, 0)]   // East
        public void DecodeDelta_ReturnsUnitVectorForValidBytes(int raw, int expectedDx, int expectedDy)
        {
            var delta = FacingByteDecoder.DecodeDelta(raw);
            Assert.NotNull(delta);
            Assert.Equal(expectedDx, delta.Value.dx);
            Assert.Equal(expectedDy, delta.Value.dy);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(4)]
        [InlineData(99)]
        public void DecodeDelta_ReturnsNullForOutOfRange(int raw)
        {
            Assert.Null(FacingByteDecoder.DecodeDelta(raw));
        }

        [Fact]
        public void AllFourCardinals_HaveDistinctDeltas()
        {
            var s = FacingByteDecoder.DecodeDelta(0);
            var w = FacingByteDecoder.DecodeDelta(1);
            var n = FacingByteDecoder.DecodeDelta(2);
            var e = FacingByteDecoder.DecodeDelta(3);
            Assert.NotEqual(s, w);
            Assert.NotEqual(s, n);
            Assert.NotEqual(s, e);
            Assert.NotEqual(w, n);
            Assert.NotEqual(w, e);
            Assert.NotEqual(n, e);
        }

        // Additional edge cases (session 33 batch 5).

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void Delta_HasUnitMagnitude(int raw)
        {
            // Every valid facing delta is a cardinal unit vector — |dx|+|dy| = 1.
            var d = FacingByteDecoder.DecodeDelta(raw);
            Assert.NotNull(d);
            int mag = System.Math.Abs(d.Value.dx) + System.Math.Abs(d.Value.dy);
            Assert.Equal(1, mag);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void NameAndDelta_ReturnEitherBothNullOrBothNonNull(int raw)
        {
            var name = FacingByteDecoder.DecodeName(raw);
            var delta = FacingByteDecoder.DecodeDelta(raw);
            Assert.Equal(name == null, delta == null);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(4)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public void NameAndDelta_BothNullForOutOfRange(int raw)
        {
            Assert.Null(FacingByteDecoder.DecodeName(raw));
            Assert.Null(FacingByteDecoder.DecodeDelta(raw));
        }

        [Fact]
        public void AllFourCardinals_NamesAreDistinct()
        {
            var s = FacingByteDecoder.DecodeName(0);
            var w = FacingByteDecoder.DecodeName(1);
            var n = FacingByteDecoder.DecodeName(2);
            var e = FacingByteDecoder.DecodeName(3);
            var names = new[] { s, w, n, e };
            Assert.Equal(4, System.Linq.Enumerable.Distinct(names).Count());
        }

        [Fact]
        public void OppositePairs_HaveOppositeDeltas()
        {
            // North and South should cancel; East and West should cancel.
            var s = FacingByteDecoder.DecodeDelta(0)!.Value;
            var n = FacingByteDecoder.DecodeDelta(2)!.Value;
            Assert.Equal(0, s.dx + n.dx);
            Assert.Equal(0, s.dy + n.dy);

            var w = FacingByteDecoder.DecodeDelta(1)!.Value;
            var e = FacingByteDecoder.DecodeDelta(3)!.Value;
            Assert.Equal(0, w.dx + e.dx);
            Assert.Equal(0, w.dy + e.dy);
        }
    }

    public class ParseFacingDirectionTests
    {
        // Tests the battle_wait <direction> arg parser. FFT grid convention
        // (matches FacingByteDecoder + FacingDecider.NameFor):
        //   (1,0)=East, (-1,0)=West, (0,-1)=North, (0,+1)=South.

        [Theory]
        [InlineData("N", 0, -1)]
        [InlineData("n", 0, -1)]
        [InlineData("North", 0, -1)]
        [InlineData("north", 0, -1)]
        [InlineData("NORTH", 0, -1)]
        [InlineData(" north ", 0, -1)]
        [InlineData("S", 0, 1)]
        [InlineData("South", 0, 1)]
        [InlineData("E", 1, 0)]
        [InlineData("East", 1, 0)]
        [InlineData("W", -1, 0)]
        [InlineData("West", -1, 0)]
        public void ParseFacingDirection_Accepts_CardinalAbbreviationsAndFullNames(
            string input, int expectedDx, int expectedDy)
        {
            var result = FFTColorCustomizer.GameBridge.NavigationActions.ParseFacingDirection(input);
            Assert.NotNull(result);
            Assert.Equal(expectedDx, result.Value.dx);
            Assert.Equal(expectedDy, result.Value.dy);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("up")]
        [InlineData("Northwest")]
        [InlineData("1")]
        public void ParseFacingDirection_ReturnsNull_ForInvalidInput(string? input)
        {
            Assert.Null(FFTColorCustomizer.GameBridge.NavigationActions.ParseFacingDirection(input));
        }
    }
}
