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

        // Additional edge cases (session 33 batch 6).

        [Theory]
        [InlineData(-1, "Shop-1")]
        [InlineData(255, "Shop255")]
        [InlineData(int.MaxValue)]
        public void ForIndex_OutOfRange_UsesShopNFallback(int index, string expected = null!)
        {
            var result = ShopTypeLabels.ForIndex(index);
            if (expected != null)
                Assert.Equal(expected, result);
            else
                Assert.StartsWith("Shop", result);
        }

        [Fact]
        public void ForIndex_AllFiveKnown_AreDistinct()
        {
            var labels = new[] {
                ShopTypeLabels.ForIndex(0),
                ShopTypeLabels.ForIndex(1),
                ShopTypeLabels.ForIndex(2),
                ShopTypeLabels.ForIndex(3),
                ShopTypeLabels.ForIndex(4),
            };
            Assert.Equal(labels.Length, System.Linq.Enumerable.Distinct(labels).Count());
        }

        [Fact]
        public void ForIndex_NeverReturnsNullOrEmpty()
        {
            // Every input value gets a non-empty string — essential for UI rendering.
            for (int i = -2; i <= 10; i++)
            {
                var label = ShopTypeLabels.ForIndex(i);
                Assert.False(string.IsNullOrEmpty(label), $"index {i} returned null/empty");
            }
        }

        [Fact]
        public void ForIndex_KnownLabels_HaveNoSpaces()
        {
            // Labels are used as screen.UI values and as state names; keep them
            // single-token for predictable grep/match.
            for (int i = 0; i <= 4; i++)
            {
                var label = ShopTypeLabels.ForIndex(i);
                Assert.DoesNotContain(" ", label);
            }
        }
    }
}
