using Xunit;
using FFTColorMod.Configuration;

namespace FFTColorMod.Tests
{
    public class TestColorSchemeEnum
    {
        [Fact]
        public void CanAccessColorSchemeEnum()
        {
            var scheme = (Configuration.ColorScheme)0; // original
            Assert.Equal((Configuration.ColorScheme)0, scheme);
        }
    }
}