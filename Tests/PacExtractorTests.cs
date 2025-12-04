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

        [Fact]
        public void GetFileCount_ReturnsZeroBeforeOpening()
        {
            // TLDR: Test file count is zero before opening PAC
            var extractor = new PacExtractor();
            var count = extractor.GetFileCount();
            Assert.Equal(0, count);
        }

        [Fact]
        public void OpenPac_WithEmptyPath_ReturnsFalse()
        {
            // TLDR: Test empty path returns false
            var extractor = new PacExtractor();
            var result = extractor.OpenPac("");
            Assert.False(result);
        }

        [Fact]
        public void GetFileName_ReturnsNullBeforeOpening()
        {
            // TLDR: Test file name returns null before opening
            var extractor = new PacExtractor();
            var name = extractor.GetFileName(0);
            Assert.Null(name);
        }

        [Fact]
        public void GetFileSize_ReturnsZeroBeforeOpening()
        {
            // TLDR: Test file size returns 0 before opening
            var extractor = new PacExtractor();
            var size = extractor.GetFileSize(0);
            Assert.Equal(0, size);
        }

        [Fact]
        public void ExtractFile_ReturnsNullBeforeOpening()
        {
            // TLDR: Test extract returns null before opening
            var extractor = new PacExtractor();
            var data = extractor.ExtractFile(0);
            Assert.Null(data);
        }
    }
}