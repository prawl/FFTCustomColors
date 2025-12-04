using Xunit;
using System.IO;

namespace FFTColorMod.Tests
{
    public class PacExtractorTests
    {
        [Fact]
        public void Constructor_CreatesInstance()
        {
            // TLDR: Test that PacExtractor can be instantiated
            var extractor = new PacExtractor();
            Assert.NotNull(extractor);
        }

        [Fact]
        public void OpenPac_WithValidPath_ReturnsTrue()
        {
            // TLDR: Test opening a PAC file
            var extractor = new PacExtractor();
            var result = extractor.OpenPac("test.pac");
            Assert.True(result);
        }

        [Fact]
        public void OpenPac_WithNullPath_ReturnsFalse()
        {
            // TLDR: Test null path returns false
            var extractor = new PacExtractor();
            var result = extractor.OpenPac(null);
            Assert.False(result);
        }
    }
}