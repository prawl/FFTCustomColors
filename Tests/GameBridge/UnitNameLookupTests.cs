using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class UnitNameLookupTests
    {
        [Theory]
        [InlineData(1, "Ramza")]
        [InlineData(22, "Mustadio")]
        [InlineData(30, "Agrias")]
        [InlineData(13, "Orlandeau")]
        [InlineData(41, "Rapha")]
        [InlineData(26, "Marach")]
        [InlineData(42, "Meliadoul")]
        [InlineData(31, "Beowulf")]
        [InlineData(15, "Reis")]
        [InlineData(50, "Cloud")]
        [InlineData(117, "Construct 8")]
        public void GetName_StoryCharacter_ReturnsName(int nameId, string expected)
        {
            var name = UnitNameLookup.GetName(nameId);
            Assert.Equal(expected, name);
        }

        [Fact]
        public void GetName_UnknownId_ReturnsNull()
        {
            var name = UnitNameLookup.GetName(298);
            Assert.Null(name);
        }

        [Fact]
        public void GetName_Zero_ReturnsNull()
        {
            var name = UnitNameLookup.GetName(0);
            Assert.Null(name);
        }
    }
}
