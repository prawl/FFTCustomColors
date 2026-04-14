using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class ShopTypeLabelsTests
    {
        [Theory]
        [InlineData(0, "Outfitter")]
        [InlineData(1, "Tavern")]
        [InlineData(2, "WarriorsGuild")]
        [InlineData(3, "PoachersDen")]
        [InlineData(4, "SaveGame")]
        public void ForIndex_ReturnsMappedLabel(int index, string expected)
        {
            Assert.Equal(expected, ShopTypeLabels.ForIndex(index));
        }

        [Fact]
        public void ForIndex_UnmappedValues_FallBackToShopN()
        {
            // Unknown shopTypeIndex values should render as "Shop{N}" so
            // Claude has a handle to work with instead of blank UI.
            Assert.Equal("Shop5", ShopTypeLabels.ForIndex(5));
            Assert.Equal("Shop99", ShopTypeLabels.ForIndex(99));
        }
    }
}
