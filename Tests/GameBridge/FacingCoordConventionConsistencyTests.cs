using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pin the coord-convention agreement across the three facing helpers:
    ///   - FacingByteDecoder.DecodeDelta(raw byte → (dx,dy))
    ///   - FacingDecider.NameFor((dx,dy) → name)
    ///   - NavigationActions.ParseFacingDirection(name → (dx,dy))
    ///
    /// FFT grid convention (verified via live memory reads 2026-04-25):
    /// +y is south. So North = (0,-1), South = (0,+1).
    ///
    /// History: FacingDecider.NameFor was flipped in commit c36ec53 to match
    /// FacingByteDecoder (which already used +y=south). ParseFacingDirection
    /// didn't get flipped until commit 219e60a — 12 days of drift where
    /// `battle_wait N` silently faced Ramza in the wrong direction.
    /// These tests prevent that drift from recurring.
    /// </summary>
    public class FacingCoordConventionConsistencyTests
    {
        [Theory]
        [InlineData(0, "South", 0, 1)]    // byte 0 = South, +y
        [InlineData(1, "West",  -1, 0)]   // byte 1 = West, -x
        [InlineData(2, "North", 0, -1)]   // byte 2 = North, -y
        [InlineData(3, "East",  1, 0)]    // byte 3 = East, +x
        public void AllThreeHelpers_AgreeForCardinal(
            int rawByte, string name, int expectedDx, int expectedDy)
        {
            // 1. FacingByteDecoder: byte → (dx,dy)
            var byteDelta = FacingByteDecoder.DecodeDelta(rawByte);
            Assert.NotNull(byteDelta);
            Assert.Equal(expectedDx, byteDelta.Value.dx);
            Assert.Equal(expectedDy, byteDelta.Value.dy);

            // 2. FacingByteDecoder: byte → name
            Assert.Equal(name, FacingByteDecoder.DecodeName(rawByte));

            // 3. FacingDecider.NameFor: (dx,dy) → name
            Assert.Equal(name, FacingDecider.NameFor(expectedDx, expectedDy));

            // 4. ParseFacingDirection: name → (dx,dy)
            var parsed = NavigationActions.ParseFacingDirection(name);
            Assert.NotNull(parsed);
            Assert.Equal(expectedDx, parsed.Value.dx);
            Assert.Equal(expectedDy, parsed.Value.dy);

            // 5. Single-letter abbreviation also parses to the same delta.
            var abbrev = NavigationActions.ParseFacingDirection(name.Substring(0, 1));
            Assert.NotNull(abbrev);
            Assert.Equal(expectedDx, abbrev.Value.dx);
            Assert.Equal(expectedDy, abbrev.Value.dy);
        }

        [Fact]
        public void RoundTrip_ByteToDeltaToNameToDelta_Stable()
        {
            // byte → delta → name → delta produces the same delta.
            for (int b = 0; b <= 3; b++)
            {
                var delta1 = FacingByteDecoder.DecodeDelta(b);
                Assert.NotNull(delta1);
                var name = FacingDecider.NameFor(delta1.Value.dx, delta1.Value.dy);
                var delta2 = NavigationActions.ParseFacingDirection(name);
                Assert.NotNull(delta2);
                Assert.Equal(delta1.Value.dx, delta2.Value.dx);
                Assert.Equal(delta1.Value.dy, delta2.Value.dy);
            }
        }
    }
}
