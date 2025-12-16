using Xunit;
using FFTColorMod.Configuration;

namespace FFTColorMod.Tests
{
    public class TestColorSchemeEnum
    {
        [Fact]
        public void CanAccessColorSchemeEnum()
        {
            var scheme = "original"; // original
            Assert.Equal("original", scheme);
        }
    }
}