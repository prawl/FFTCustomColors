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
    }
}
