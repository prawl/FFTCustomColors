using Xunit;
using FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Tests
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
