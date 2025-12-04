using Xunit;
using System.IO;
using System.Collections.Generic;

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
        public void OpenPac_WithExistingFile_ReturnsTrue()
        {
            // TLDR: Test opening an existing PAC file
            var extractor = new PacExtractor();

            // Create a temporary test file
            var tempFile = Path.GetTempFileName();
            try
            {
                var result = extractor.OpenPac(tempFile);
                Assert.True(result);
            }
            finally
            {
                File.Delete(tempFile);
            }
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

        [Fact]
        public void OpenPac_WithNonExistentFile_ReturnsFalse()
        {
            // TLDR: Test opening non-existent file returns false
            var extractor = new PacExtractor();
            var result = extractor.OpenPac("nonexistent.pac");
            Assert.False(result);
        }

        [Fact]
        public void OpenPac_SetsFileCount()
        {
            // TLDR: Test that OpenPac reads file count
            var extractor = new PacExtractor();
            var tempFile = Path.GetTempFileName();
            try
            {
                // Write 4 bytes for file count
                File.WriteAllBytes(tempFile, new byte[] { 0x05, 0x00, 0x00, 0x00 }); // 5 files
                extractor.OpenPac(tempFile);
                Assert.Equal(5, extractor.GetFileCount());
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void GetFileCount_AfterOpeningFile_ReturnsNonZero()
        {
            // TLDR: Test file count after opening a PAC file
            var extractor = new PacExtractor();

            // Create a test PAC file with minimal header
            var tempFile = Path.GetTempFileName();
            try
            {
                // Write minimal PAC header (4 bytes for file count, little endian)
                File.WriteAllBytes(tempFile, new byte[] { 0x01, 0x00, 0x00, 0x00 }); // 1 file

                var opened = extractor.OpenPac(tempFile);
                Assert.True(opened, "Failed to open test PAC file");

                var count = extractor.GetFileCount();
                Assert.Equal(1, count);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void GetFileName_AfterOpeningFile_ReturnsFileName()
        {
            // TLDR: Test getting file name from PAC
            var extractor = new PacExtractor();
            var tempFile = Path.GetTempFileName();
            try
            {
                // PAC format: [4 bytes count][file entries][file data]
                // File entry: [4 bytes offset][4 bytes size][32 bytes name]
                var pacData = new List<byte>();

                // File count (1 file)
                pacData.AddRange(BitConverter.GetBytes(1));

                // File entry: offset=44, size=100, name="test.spr"
                pacData.AddRange(BitConverter.GetBytes(44)); // offset after header
                pacData.AddRange(BitConverter.GetBytes(100)); // size
                pacData.AddRange(System.Text.Encoding.ASCII.GetBytes("test.spr".PadRight(32, '\0')));

                File.WriteAllBytes(tempFile, pacData.ToArray());

                extractor.OpenPac(tempFile);
                var fileName = extractor.GetFileName(0);
                Assert.Equal("test.spr", fileName);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void ExtractFile_AfterOpeningFile_ReturnsFileData()
        {
            // TLDR: Test extracting file data from PAC
            var extractor = new PacExtractor();
            var tempFile = Path.GetTempFileName();
            try
            {
                var pacData = new List<byte>();

                // File count (1 file)
                pacData.AddRange(BitConverter.GetBytes(1));

                // File entry: offset=44, size=4, name="test.spr"
                int fileDataOffset = 44;
                int fileSize = 4;
                pacData.AddRange(BitConverter.GetBytes(fileDataOffset));
                pacData.AddRange(BitConverter.GetBytes(fileSize));
                pacData.AddRange(System.Text.Encoding.ASCII.GetBytes("test.spr".PadRight(32, '\0')));

                // File data
                byte[] testData = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
                pacData.AddRange(testData);

                File.WriteAllBytes(tempFile, pacData.ToArray());

                extractor.OpenPac(tempFile);
                var extractedData = extractor.ExtractFile(0);

                Assert.NotNull(extractedData);
                Assert.Equal(testData, extractedData);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void ExtractAllSprites_ExtractsOnlySprFiles()
        {
            // TLDR: Test extracting only .SPR files from PAC
            var extractor = new PacExtractor();
            var tempFile = Path.GetTempFileName();
            var outputDir = Path.Combine(Path.GetTempPath(), "test_sprites");

            try
            {
                var pacData = new List<byte>();

                // File count (3 files: 2 SPR, 1 other)
                pacData.AddRange(BitConverter.GetBytes(3));

                // File 1: test.spr at offset 124
                pacData.AddRange(BitConverter.GetBytes(124));
                pacData.AddRange(BitConverter.GetBytes(4));
                pacData.AddRange(System.Text.Encoding.ASCII.GetBytes("test.spr".PadRight(32, '\0')));

                // File 2: other.bin at offset 128
                pacData.AddRange(BitConverter.GetBytes(128));
                pacData.AddRange(BitConverter.GetBytes(4));
                pacData.AddRange(System.Text.Encoding.ASCII.GetBytes("other.bin".PadRight(32, '\0')));

                // File 3: sprite2.spr at offset 132
                pacData.AddRange(BitConverter.GetBytes(132));
                pacData.AddRange(BitConverter.GetBytes(4));
                pacData.AddRange(System.Text.Encoding.ASCII.GetBytes("sprite2.spr".PadRight(32, '\0')));

                // File data
                pacData.AddRange(new byte[] { 0x01, 0x02, 0x03, 0x04 }); // test.spr
                pacData.AddRange(new byte[] { 0x05, 0x06, 0x07, 0x08 }); // other.bin
                pacData.AddRange(new byte[] { 0x09, 0x0A, 0x0B, 0x0C }); // sprite2.spr

                File.WriteAllBytes(tempFile, pacData.ToArray());

                extractor.OpenPac(tempFile);
                var extractedCount = extractor.ExtractAllSprites(outputDir);

                Assert.Equal(2, extractedCount); // Only 2 SPR files
                Assert.True(File.Exists(Path.Combine(outputDir, "test.spr")));
                Assert.True(File.Exists(Path.Combine(outputDir, "sprite2.spr")));
                Assert.False(File.Exists(Path.Combine(outputDir, "other.bin")));
            }
            finally
            {
                File.Delete(tempFile);
                if (Directory.Exists(outputDir))
                    Directory.Delete(outputDir, true);
            }
        }
    }
}