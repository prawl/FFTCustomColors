using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class BattleCursorFormatterTests
    {
        [Theory]
        [InlineData(0, 0, "(0,0)")]
        [InlineData(10, 9, "(10,9)")]
        [InlineData(7, 3, "(7,3)")]
        public void FormatCursor_ReturnsParenthesizedPair(int x, int y, string expected)
        {
            Assert.Equal(expected, BattleCursorFormatter.FormatCursor(x, y));
        }
    }
}
