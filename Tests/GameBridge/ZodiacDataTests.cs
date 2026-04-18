using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class ZodiacDataTests
    {
        [Fact]
        public void GetByNameId_Agrias_ReturnsCancer()
        {
            // Agrias = nameId 30 per CharacterData.StoryCharacterName
            var sign = ZodiacData.GetByNameId(30);
            Assert.Equal(ZodiacData.Sign.Cancer, sign);
        }

        [Fact]
        public void GetByNameId_Mustadio_ReturnsLibra()
        {
            var sign = ZodiacData.GetByNameId(22);
            Assert.Equal(ZodiacData.Sign.Libra, sign);
        }

        [Fact]
        public void GetByNameId_Cloud_ReturnsAquarius()
        {
            var sign = ZodiacData.GetByNameId(50);
            Assert.Equal(ZodiacData.Sign.Aquarius, sign);
        }

        [Fact]
        public void GetByNameId_Orlandeau_ReturnsScorpio()
        {
            var sign = ZodiacData.GetByNameId(13);
            Assert.Equal(ZodiacData.Sign.Scorpio, sign);
        }

        [Fact]
        public void GetByNameId_Generic_ReturnsNull()
        {
            // NameId that's never a story character
            Assert.Null(ZodiacData.GetByNameId(9999));
        }

        [Fact]
        public void GetByNameId_Ramza_ReturnsNull()
        {
            // Ramza's zodiac is player-chosen at game start, not canonical.
            // Nevertheless he shouldn't crash — just return null.
            Assert.Null(ZodiacData.GetByNameId(1));
        }

        [Fact]
        public void GetSignName_Virgo_ReturnsString()
        {
            Assert.Equal("Virgo", ZodiacData.GetSignName(ZodiacData.Sign.Virgo));
        }

        [Fact]
        public void GetSignName_Serpentarius_ReturnsString()
        {
            Assert.Equal("Serpentarius", ZodiacData.GetSignName(ZodiacData.Sign.Serpentarius));
        }

        [Fact]
        public void GetOpposite_Aries_ReturnsLibra()
        {
            Assert.Equal(ZodiacData.Sign.Libra, ZodiacData.GetOpposite(ZodiacData.Sign.Aries));
        }

        [Fact]
        public void GetOpposite_Virgo_ReturnsPisces()
        {
            Assert.Equal(ZodiacData.Sign.Pisces, ZodiacData.GetOpposite(ZodiacData.Sign.Virgo));
        }

        [Fact]
        public void GetOpposite_Serpentarius_ReturnsNull()
        {
            Assert.Null(ZodiacData.GetOpposite(ZodiacData.Sign.Serpentarius));
        }

        // Complete opposite-pair coverage (session 33 batch 5).
        [Theory]
        [InlineData(ZodiacData.Sign.Aries, ZodiacData.Sign.Libra)]
        [InlineData(ZodiacData.Sign.Libra, ZodiacData.Sign.Aries)]
        [InlineData(ZodiacData.Sign.Taurus, ZodiacData.Sign.Scorpio)]
        [InlineData(ZodiacData.Sign.Scorpio, ZodiacData.Sign.Taurus)]
        [InlineData(ZodiacData.Sign.Gemini, ZodiacData.Sign.Sagittarius)]
        [InlineData(ZodiacData.Sign.Sagittarius, ZodiacData.Sign.Gemini)]
        [InlineData(ZodiacData.Sign.Cancer, ZodiacData.Sign.Capricorn)]
        [InlineData(ZodiacData.Sign.Capricorn, ZodiacData.Sign.Cancer)]
        [InlineData(ZodiacData.Sign.Leo, ZodiacData.Sign.Aquarius)]
        [InlineData(ZodiacData.Sign.Aquarius, ZodiacData.Sign.Leo)]
        [InlineData(ZodiacData.Sign.Virgo, ZodiacData.Sign.Pisces)]
        [InlineData(ZodiacData.Sign.Pisces, ZodiacData.Sign.Virgo)]
        public void GetOpposite_AllPairs(ZodiacData.Sign sign, ZodiacData.Sign expected)
        {
            Assert.Equal(expected, ZodiacData.GetOpposite(sign));
        }

        [Theory]
        [InlineData(ZodiacData.Sign.Aries)]
        [InlineData(ZodiacData.Sign.Taurus)]
        [InlineData(ZodiacData.Sign.Gemini)]
        [InlineData(ZodiacData.Sign.Cancer)]
        [InlineData(ZodiacData.Sign.Leo)]
        [InlineData(ZodiacData.Sign.Virgo)]
        [InlineData(ZodiacData.Sign.Libra)]
        [InlineData(ZodiacData.Sign.Scorpio)]
        [InlineData(ZodiacData.Sign.Sagittarius)]
        [InlineData(ZodiacData.Sign.Capricorn)]
        [InlineData(ZodiacData.Sign.Aquarius)]
        [InlineData(ZodiacData.Sign.Pisces)]
        public void GetOpposite_IsInvolutionForAllTwelveSigns(ZodiacData.Sign sign)
        {
            // Opposite of opposite should return the original sign for all 12.
            var opp = ZodiacData.GetOpposite(sign);
            Assert.NotNull(opp);
            var oppOfOpp = ZodiacData.GetOpposite(opp!.Value);
            Assert.Equal(sign, oppOfOpp);
        }

        [Fact]
        public void SignNames_HasThirteenEntries()
        {
            // 12 zodiac signs + Serpentarius = 13.
            Assert.Equal(13, ZodiacData.SignNames.Length);
        }

        [Fact]
        public void GetSignName_AllTwelveZodiacSigns_ReturnNames()
        {
            for (int i = 0; i < 12; i++)
            {
                var name = ZodiacData.GetSignName((ZodiacData.Sign)i);
                Assert.False(string.IsNullOrEmpty(name),
                    $"Sign index {i} has no name");
            }
        }

        [Fact]
        public void GetSignName_OutOfRangeNegative_ReturnsNull()
        {
            Assert.Null(ZodiacData.GetSignName((ZodiacData.Sign)(-1)));
        }

        [Fact]
        public void GetSignName_OutOfRangeHigh_ReturnsNull()
        {
            Assert.Null(ZodiacData.GetSignName((ZodiacData.Sign)99));
        }

        [Theory]
        [InlineData(2, "Delita")]        // Sagittarius
        [InlineData(5, "Alma")]          // Leo
        [InlineData(4, "Ovelia")]        // Taurus
        [InlineData(15, "Reis")]         // Pisces
        [InlineData(26, "Marach")]       // Gemini
        [InlineData(31, "Beowulf")]      // Libra
        [InlineData(41, "Rapha")]        // Pisces
        [InlineData(42, "Meliadoul")]    // Capricorn
        [InlineData(117, "Construct 8")] // Gemini
        public void GetByNameId_AllKnownStoryCharacters_ReturnNonNull(int nameId, string _)
        {
            Assert.NotNull(ZodiacData.GetByNameId(nameId));
        }

        [Fact]
        public void GetByNameId_NegativeId_ReturnsNull()
        {
            Assert.Null(ZodiacData.GetByNameId(-1));
        }

        [Fact]
        public void GetByNameId_IntMaxValue_ReturnsNull()
        {
            Assert.Null(ZodiacData.GetByNameId(int.MaxValue));
        }
    }
}
