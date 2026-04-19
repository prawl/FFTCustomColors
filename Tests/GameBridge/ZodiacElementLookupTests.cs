using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for <see cref="ZodiacElementLookup.GetElement"/> and its
    /// invariant relationship with <see cref="ZodiacData.GetCompatibility"/>:
    /// two different signs of the same element ⇒ Good compatibility.
    /// </summary>
    public class ZodiacElementLookupTests
    {
        [Theory]
        [InlineData(ZodiacData.Sign.Aries, ZodiacElement.Fire)]
        [InlineData(ZodiacData.Sign.Leo, ZodiacElement.Fire)]
        [InlineData(ZodiacData.Sign.Sagittarius, ZodiacElement.Fire)]
        [InlineData(ZodiacData.Sign.Taurus, ZodiacElement.Earth)]
        [InlineData(ZodiacData.Sign.Virgo, ZodiacElement.Earth)]
        [InlineData(ZodiacData.Sign.Capricorn, ZodiacElement.Earth)]
        [InlineData(ZodiacData.Sign.Gemini, ZodiacElement.Air)]
        [InlineData(ZodiacData.Sign.Libra, ZodiacElement.Air)]
        [InlineData(ZodiacData.Sign.Aquarius, ZodiacElement.Air)]
        [InlineData(ZodiacData.Sign.Cancer, ZodiacElement.Water)]
        [InlineData(ZodiacData.Sign.Scorpio, ZodiacElement.Water)]
        [InlineData(ZodiacData.Sign.Pisces, ZodiacElement.Water)]
        [InlineData(ZodiacData.Sign.Serpentarius, ZodiacElement.None)]
        public void GetElement_ReturnsExpected(ZodiacData.Sign sign, ZodiacElement expected)
        {
            Assert.Equal(expected, ZodiacElementLookup.GetElement(sign));
        }

        [Fact]
        public void SameElementDifferentSigns_AreGoodCompatibility()
        {
            // Invariant: every pair of distinct signs in the same element
            // group should be Good per ZodiacData.GetCompatibility.
            foreach (ZodiacData.Sign a in System.Enum.GetValues(typeof(ZodiacData.Sign)))
            {
                if (a == ZodiacData.Sign.Serpentarius) continue;
                foreach (ZodiacData.Sign b in System.Enum.GetValues(typeof(ZodiacData.Sign)))
                {
                    if (b == ZodiacData.Sign.Serpentarius) continue;
                    if (a == b) continue;
                    if (ZodiacElementLookup.GetElement(a) != ZodiacElementLookup.GetElement(b))
                        continue;

                    // Same element, different sign → Good (gender-agnostic for this column).
                    var compat = ZodiacData.GetCompatibility(a, b, sameGender: true);
                    Assert.Equal(ZodiacData.Compatibility.Good, compat);
                }
            }
        }

        [Fact]
        public void Serpentarius_IsNone()
        {
            Assert.Equal(ZodiacElement.None, ZodiacElementLookup.GetElement(ZodiacData.Sign.Serpentarius));
        }

        [Fact]
        public void EverySign_HasAnElement()
        {
            // Completeness: every enum value in Sign must have a mapping.
            foreach (ZodiacData.Sign sign in System.Enum.GetValues(typeof(ZodiacData.Sign)))
            {
                var element = ZodiacElementLookup.GetElement(sign);
                // Serpentarius is allowed to be None; all others must NOT be.
                if (sign == ZodiacData.Sign.Serpentarius)
                    Assert.Equal(ZodiacElement.None, element);
                else
                    Assert.NotEqual(ZodiacElement.None, element);
            }
        }
    }
}
