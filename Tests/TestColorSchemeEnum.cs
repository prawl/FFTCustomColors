using Xunit;
using FFTColorMod.Configuration;

namespace FFTColorMod.Tests
{
    public class TestColorSchemeEnum
    {
        [Fact]
        public void CanAccessColorSchemeEnum()
        {
            var scheme = (FFTColorMod.Configuration.ColorScheme)0; // original
            Assert.Equal((FFTColorMod.Configuration.ColorScheme)0, scheme);
        }
    }
}