using Xunit;
using FFTColorMod.Configuration;
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

        [Fact]
        public void ExtractSpritesFromDirectory_ProcessesAllPacFiles()
        {
            // TLDR: Test extracting sprites from multiple PAC files
            var tempDir = Path.Combine(Path.GetTempPath(), "test_pacs");
            var outputDir = Path.Combine(Path.GetTempPath(), "extracted_sprites");

            try
            {
                Directory.CreateDirectory(tempDir);

                // Create test PAC file 1
                var pacData1 = new List<byte>();
                pacData1.AddRange(BitConverter.GetBytes(1));
                pacData1.AddRange(BitConverter.GetBytes(44));
                pacData1.AddRange(BitConverter.GetBytes(4));
                pacData1.AddRange(System.Text.Encoding.ASCII.GetBytes("sprite1.spr".PadRight(32, '\0')));
                pacData1.AddRange(new byte[] { 0x01, 0x02, 0x03, 0x04 });
                File.WriteAllBytes(Path.Combine(tempDir, "test1.pac"), pacData1.ToArray());

                // Create test PAC file 2
                var pacData2 = new List<byte>();
                pacData2.AddRange(BitConverter.GetBytes(1));
                pacData2.AddRange(BitConverter.GetBytes(44));
                pacData2.AddRange(BitConverter.GetBytes(4));
                pacData2.AddRange(System.Text.Encoding.ASCII.GetBytes("sprite2.spr".PadRight(32, '\0')));
                pacData2.AddRange(new byte[] { 0x05, 0x06, 0x07, 0x08 });
                File.WriteAllBytes(Path.Combine(tempDir, "test2.pac"), pacData2.ToArray());

                int totalExtracted = PacExtractor.ExtractSpritesFromDirectory(tempDir, outputDir);

                Assert.Equal(2, totalExtracted);
                Assert.True(Directory.Exists(Path.Combine(outputDir, "test1")));
                Assert.True(Directory.Exists(Path.Combine(outputDir, "test2")));
                Assert.True(File.Exists(Path.Combine(outputDir, "test1", "sprite1.spr")));
                Assert.True(File.Exists(Path.Combine(outputDir, "test2", "sprite2.spr")));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                if (Directory.Exists(outputDir))
                    Directory.Delete(outputDir, true);
            }
        }

        [Fact]
        public void ExtractAllSprites_ExtractsBinFiles()
        {
            // TLDR: Test extracting .bin files (FFT sprite format) from PAC
            var extractor = new PacExtractor();
            var tempFile = Path.GetTempFileName();
            var outputDir = Path.Combine(Path.GetTempPath(), "test_bin_sprites");

            try
            {
                var pacData = new List<byte>();

                // File count (1 file)
                pacData.AddRange(BitConverter.GetBytes(1));

                // File 1: battle_ramza_spr.bin at offset 44
                pacData.AddRange(BitConverter.GetBytes(44));
                pacData.AddRange(BitConverter.GetBytes(4));
                pacData.AddRange(System.Text.Encoding.ASCII.GetBytes("battle_ramza_spr.bin".PadRight(32, '\0')));

                // File data
                pacData.AddRange(new byte[] { 0x01, 0x02, 0x03, 0x04 });

                File.WriteAllBytes(tempFile, pacData.ToArray());

                extractor.OpenPac(tempFile);
                var extractedCount = extractor.ExtractAllSprites(outputDir);

                // Should extract the .bin file
                Assert.Equal(1, extractedCount);
                Assert.True(File.Exists(Path.Combine(outputDir, "battle_ramza_spr.bin")));
            }
            finally
            {
                File.Delete(tempFile);
                if (Directory.Exists(outputDir))
                    Directory.Delete(outputDir, true);
            }
        }

        [Fact]
        public void OpenPacStream_HandlesLargeFiles()
        {
            // TLDR: Test that PacExtractor can handle large files using streaming
            var extractor = new PacExtractor();
            var tempFile = Path.GetTempFileName();

            try
            {
                // Create a small test PAC file (simulating the structure)
                var pacData = new List<byte>();
                pacData.AddRange(BitConverter.GetBytes(1)); // file count
                pacData.AddRange(BitConverter.GetBytes(44)); // offset
                pacData.AddRange(BitConverter.GetBytes(4));  // size
                pacData.AddRange(System.Text.Encoding.ASCII.GetBytes("test.spr".PadRight(32, '\0')));
                pacData.AddRange(new byte[] { 0x01, 0x02, 0x03, 0x04 });

                File.WriteAllBytes(tempFile, pacData.ToArray());

                // Test streaming approach
                var result = extractor.OpenPacStream(tempFile);
                Assert.True(result);
            }
            finally
            {
                extractor.ClosePac();
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void ExtractSpritesUsingStream_ExtractsFromLargePac()
        {
            // TLDR: Test extracting sprites using streaming for large files
            var extractor = new PacExtractor();
            var tempFile = Path.GetTempFileName();
            var outputDir = Path.Combine(Path.GetTempPath(), "stream_sprites");

            try
            {
                // Create test PAC with sprite
                var pacData = new List<byte>();
                pacData.AddRange(BitConverter.GetBytes(1)); // file count
                pacData.AddRange(BitConverter.GetBytes(44)); // offset
                pacData.AddRange(BitConverter.GetBytes(4));  // size
                pacData.AddRange(System.Text.Encoding.ASCII.GetBytes("battle_test_spr.bin".PadRight(32, '\0')));
                pacData.AddRange(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD });

                File.WriteAllBytes(tempFile, pacData.ToArray());

                // Open with streaming
                var opened = extractor.OpenPacStream(tempFile);
                Assert.True(opened);

                // Extract sprites using stream
                var count = extractor.ExtractSpritesUsingStream(outputDir);

                // Should extract 1 sprite
                Assert.Equal(1, count);
                Assert.True(File.Exists(Path.Combine(outputDir, "battle_test_spr.bin")));
            }
            finally
            {
                extractor.ClosePac();
                File.Delete(tempFile);
                if (Directory.Exists(outputDir))
                    Directory.Delete(outputDir, true);
            }
        }

        [Fact]
        public void ExtractSpritesUsingStream_HandlesFFTPackFormat()
        {
            // TLDR: Test extracting from FFT's PACK header format
            var extractor = new PacExtractor();
            var tempFile = Path.GetTempFileName();
            var outputDir = Path.Combine(Path.GetTempPath(), "pack_sprites");

            try
            {
                // Create FFT PACK format PAC file
                var pacData = new List<byte>();
                pacData.AddRange(System.Text.Encoding.ASCII.GetBytes("PACK")); // PACK header
                pacData.AddRange(BitConverter.GetBytes(1)); // file count at offset 4

                // File entry at offset 8
                pacData.AddRange(BitConverter.GetBytes(48)); // data offset
                pacData.AddRange(BitConverter.GetBytes(5));  // size
                pacData.AddRange(System.Text.Encoding.ASCII.GetBytes("ramza_spr.bin".PadRight(32, '\0')));

                // File data at offset 48
                pacData.AddRange(new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55 });

                File.WriteAllBytes(tempFile, pacData.ToArray());

                // Open and extract using streaming
                var opened = extractor.OpenPacStream(tempFile);
                Assert.True(opened);

                var count = extractor.ExtractSpritesUsingStream(outputDir);

                // Should extract the sprite
                Assert.Equal(1, count);
                Assert.True(File.Exists(Path.Combine(outputDir, "ramza_spr.bin")));

                // Verify file contents
                var extracted = File.ReadAllBytes(Path.Combine(outputDir, "ramza_spr.bin"));
                Assert.Equal(5, extracted.Length);
                Assert.Equal(0x11, extracted[0]);
            }
            finally
            {
                extractor.ClosePac();
                File.Delete(tempFile);
                if (Directory.Exists(outputDir))
                    Directory.Delete(outputDir, true);
            }
        }

        [Fact]
        public void ExtractSpritesFromGameDirectory_FindsRamzaSprite()
        {
            // TLDR: Test searching for Ramza sprite in actual game PAC files
            var extractor = new PacExtractor();
            var gameDir = @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS\data\enhanced";
            var outputDir = Path.Combine(Path.GetTempPath(), "fft_ramza_search");

            try
            {
                // Skip test if game directory doesn't exist
                if (!Directory.Exists(gameDir))
                {
                    return; // Skip test when game not installed
                }

                // Search for Ramza sprites in PAC files
                var pacFiles = Directory.GetFiles(gameDir, "*.pac", SearchOption.AllDirectories);
                bool ramzaFound = false;

                foreach (var pacFile in pacFiles)
                {
                    if (extractor.OpenPacStream(pacFile))
                    {
                        // Check if this PAC contains Ramza sprite
                        var fileCount = extractor.GetFileCount();
                        for (int i = 0; i < fileCount; i++)
                        {
                            var fileName = extractor.GetFileName(i);
                            if (fileName != null && fileName.Contains("ramza", StringComparison.OrdinalIgnoreCase)
                                && fileName.EndsWith("_spr.bin", StringComparison.OrdinalIgnoreCase))
                            {
                                ramzaFound = true;

                                // Extract just this file for testing
                                Directory.CreateDirectory(outputDir);
                                var fileData = extractor.ExtractFile(i);
                                Assert.NotNull(fileData);

                                // Write the extracted data to file
                                var outputPath = Path.Combine(outputDir, fileName);
                                File.WriteAllBytes(outputPath, fileData);

                                // Verify the file was extracted
                                Assert.True(File.Exists(outputPath));
                                break;
                            }
                        }

                        extractor.ClosePac();

                        if (ramzaFound) break;
                    }
                }

                // We expect to find Ramza's sprite somewhere
                Assert.True(ramzaFound, "Could not find Ramza sprite in game PAC files");
            }
            finally
            {
                extractor.ClosePac();
                if (Directory.Exists(outputDir))
                    Directory.Delete(outputDir, true);
            }
        }

        [Fact]
        public void FindAndExtractSpritesUsingStream_ExtractsMatchingSprites()
        {
            // TLDR: Test the FindAndExtractSpritesUsingStream method with search pattern
            var extractor = new PacExtractor();
            var tempDir = Path.Combine(Path.GetTempPath(), "find_extract_stream_test");
            var gameDir = Path.Combine(tempDir, "game");
            var outputDir = Path.Combine(tempDir, "output");

            try
            {
                Directory.CreateDirectory(gameDir);

                // Create a test PAC file with multiple sprites
                var pacFile = Path.Combine(gameDir, "test.pac");
                var pacData = new List<byte>();
                pacData.AddRange(System.Text.Encoding.ASCII.GetBytes("PACK")); // PACK header
                pacData.AddRange(BitConverter.GetBytes(3)); // 3 files

                // File entries start at offset 8
                // Each entry is 40 bytes (4 offset + 4 size + 32 name)
                // So 3 entries = 120 bytes, starting at offset 8, ending at 128
                // Data starts at offset 128

                // File 1: ramza_spr.bin at offset 128
                pacData.AddRange(BitConverter.GetBytes(128)); // offset
                pacData.AddRange(BitConverter.GetBytes(5));   // size
                pacData.AddRange(System.Text.Encoding.ASCII.GetBytes("ramza_spr.bin".PadRight(32, '\0')));

                // File 2: delita_spr.bin at offset 133
                pacData.AddRange(BitConverter.GetBytes(133)); // offset
                pacData.AddRange(BitConverter.GetBytes(5));   // size
                pacData.AddRange(System.Text.Encoding.ASCII.GetBytes("delita_spr.bin".PadRight(32, '\0')));

                // File 3: some_other_file.dat at offset 138
                pacData.AddRange(BitConverter.GetBytes(138)); // offset
                pacData.AddRange(BitConverter.GetBytes(5));   // size
                pacData.AddRange(System.Text.Encoding.ASCII.GetBytes("some_other_file.dat".PadRight(32, '\0')));

                // File data
                pacData.AddRange(new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55 }); // ramza
                pacData.AddRange(new byte[] { 0x66, 0x77, 0x88, 0x99, 0xAA }); // delita
                pacData.AddRange(new byte[] { 0xBB, 0xCC, 0xDD, 0xEE, 0xFF }); // other

                File.WriteAllBytes(pacFile, pacData.ToArray());

                // Search for "ramza" sprites using the streaming method
                var extracted = extractor.FindAndExtractSpritesUsingStream(gameDir, "ramza", outputDir);

                // Should only extract ramza sprite
                Assert.Equal(1, extracted.Count);
                Assert.Contains("ramza_spr.bin", extracted);

                // Verify file exists and has correct content
                var outputFile = Path.Combine(outputDir, "ramza_spr.bin");
                Assert.True(File.Exists(outputFile));
                var content = File.ReadAllBytes(outputFile);
                Assert.Equal(5, content.Length);
                Assert.Equal(0x11, content[0]);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}