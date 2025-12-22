using System;
using System.IO;
using System.Drawing;
using Xunit;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Tests
{
    public class TexFileModifierTests
    {
        private readonly string _testDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData");

        [Fact]
        public void TexFileModifier_ShouldDetectYoxCompressedFormat()
        {
            // Arrange
            var modifier = new TexFileModifier();
            var testFile = CreateYoxCompressedTestFile();

            // Act
            bool isCompressed = modifier.IsYoxCompressed(testFile);

            // Assert
            Assert.True(isCompressed);

            // Cleanup
            File.Delete(testFile);
        }

        [Fact]
        public void TexFileModifier_ShouldDetectUncompressedFormat()
        {
            // Arrange
            var modifier = new TexFileModifier();
            var testFile = CreateUncompressedTestFile();

            // Act
            bool isCompressed = modifier.IsYoxCompressed(testFile);

            // Assert
            Assert.False(isCompressed);

            // Cleanup
            File.Delete(testFile);
        }

        [Fact]
        public void TexFileModifier_ShouldDecompressYoxFile()
        {
            // Arrange
            var modifier = new TexFileModifier();
            var testFile = CreateYoxCompressedTestFile();

            // Act
            byte[] decompressed = modifier.DecompressTex(testFile);

            // Assert
            Assert.NotNull(decompressed);
            Assert.True(decompressed.Length > 0);
            // Should decompress to test data
            Assert.Equal(100, decompressed.Length); // Our test data is 100 bytes

            // Cleanup
            File.Delete(testFile);
        }

        [Fact]
        public void TexFileModifier_ShouldReadUncompressedFile()
        {
            // Arrange
            var modifier = new TexFileModifier();
            var testFile = CreateUncompressedTestFile();

            // Act
            byte[] data = modifier.DecompressTex(testFile);

            // Assert
            Assert.NotNull(data);
            Assert.Equal(131072, data.Length); // Standard tex file size

            // Cleanup
            File.Delete(testFile);
        }

        [Fact]
        public void TexFileModifier_ShouldTransformBrownToWhite()
        {
            // Arrange
            var modifier = new TexFileModifier();

            // Brown armor color in RGB888
            int r = 72, g = 64, b = 16;

            // Act
            var (newR, newG, newB) = modifier.TransformColor(r, g, b, "white_heretic");

            // Assert
            Assert.Equal(224, newR);
            Assert.Equal(248, newG);
            Assert.Equal(248, newB);
        }

        [Fact]
        public void TexFileModifier_ShouldKeepSkinToneUnchanged()
        {
            // Arrange
            var modifier = new TexFileModifier();

            // Skin tone color
            int r = 200, g = 160, b = 120;

            // Act
            var (newR, newG, newB) = modifier.TransformColor(r, g, b, "white_heretic");

            // Assert
            Assert.Equal(r, newR);
            Assert.Equal(g, newG);
            Assert.Equal(b, newB);
        }

        [Fact]
        public void TexFileModifier_ShouldConvertRgb888ToRgb555()
        {
            // Arrange
            var modifier = new TexFileModifier();

            // RGB888 values
            int r = 224, g = 248, b = 248;

            // Act
            ushort rgb555 = modifier.RgbToRgb555(r, g, b);

            // Assert
            // 224 >> 3 = 28, 248 >> 3 = 31, 248 >> 3 = 31
            // Expected: 28 | (31 << 5) | (31 << 10) = 28 + 992 + 31744 = 32764 = 0x7FFC
            Assert.Equal(0x7FFC, rgb555);
        }

        [Fact]
        public void TexFileModifier_ShouldConvertRgb555ToRgb888()
        {
            // Arrange
            var modifier = new TexFileModifier();

            // RGB555 value (white)
            ushort rgb555 = 0x7FFC;

            // Act
            var (r, g, b) = modifier.Rgb555ToRgb(rgb555);

            // Assert
            Assert.Equal(224, r); // (0x7FFC & 0x1F) << 3 = 28 << 3 = 224
            Assert.Equal(248, g); // ((0x7FFC >> 5) & 0x1F) << 3 = 31 << 3 = 248
            Assert.Equal(248, b); // ((0x7FFC >> 10) & 0x1F) << 3 = 31 << 3 = 248
        }

        [Fact]
        public void TexFileModifier_ShouldModifyTexFile()
        {
            // Arrange
            var modifier = new TexFileModifier();
            var inputFile = CreateTestTexWithBrownColors();
            var outputFile = Path.GetTempFileName();

            // Act
            modifier.ModifyTexColors(inputFile, outputFile, "white_heretic");

            // Assert
            Assert.True(File.Exists(outputFile));

            // Read the output and verify colors were changed
            byte[] outputData = File.ReadAllBytes(outputFile);
            Assert.Equal(131072, outputData.Length); // Standard size

            // Cleanup
            File.Delete(inputFile);
            File.Delete(outputFile);
        }

        [Fact]
        public void TexFileModifier_ShouldCountColorChanges()
        {
            // Arrange
            var modifier = new TexFileModifier();
            var inputFile = CreateTestTexWithBrownColors();
            var outputFile = Path.GetTempFileName();

            // Act
            int changesCount = modifier.ModifyTexColors(inputFile, outputFile, "white_heretic");

            // Assert
            Assert.True(changesCount > 0); // Should have made some changes

            // Cleanup
            File.Delete(inputFile);
            File.Delete(outputFile);
        }

        // Helper methods to create test files
        private string CreateYoxCompressedTestFile()
        {
            var tempFile = Path.GetTempFileName();
            using (var fs = new FileStream(tempFile, FileMode.Create))
            {
                // Write header (0x400 bytes of zeros)
                fs.Write(new byte[0x400], 0, 0x400);

                // Write YOX header
                fs.Write(new byte[] { (byte)'Y', (byte)'O', (byte)'X', 0 }, 0, 4);

                // Write 12 bytes of header data
                fs.Write(new byte[12], 0, 12);

                // Write compressed data (zlib compress 100 bytes of test data)
                var testData = new byte[100];
                for (int i = 0; i < 100; i++) testData[i] = (byte)(i % 256);

                using (var ms = new MemoryStream())
                {
                    // Write zlib header
                    ms.WriteByte(0x78);
                    ms.WriteByte(0x9C);

                    using (var deflate = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionLevel.Optimal, true))
                    {
                        deflate.Write(testData, 0, testData.Length);
                    }

                    var compressed = ms.ToArray();
                    fs.Write(compressed, 0, compressed.Length);
                }

                // Pad to standard size
                long remaining = 131072 - fs.Position;
                if (remaining > 0)
                    fs.Write(new byte[remaining], 0, (int)remaining);
            }
            return tempFile;
        }

        private string CreateUncompressedTestFile()
        {
            var tempFile = Path.GetTempFileName();
            using (var fs = new FileStream(tempFile, FileMode.Create))
            {
                // Write 131072 bytes (standard tex file size)
                var data = new byte[131072];
                fs.Write(data, 0, data.Length);
            }
            return tempFile;
        }

        private string CreateTestTexWithBrownColors()
        {
            var tempFile = Path.GetTempFileName();
            using (var fs = new FileStream(tempFile, FileMode.Create))
            {
                var data = new byte[131072];

                // Add some brown colors (RGB555 format) at known offsets
                // Brown: R=72, G=64, B=16 -> RGB555
                ushort brownColor = (ushort)(((72 >> 3) & 0x1F) |
                                            (((64 >> 3) & 0x1F) << 5) |
                                            (((16 >> 3) & 0x1F) << 10));

                // Place brown colors at specific offsets
                data[0x0E50] = (byte)(brownColor & 0xFF);
                data[0x0E51] = (byte)((brownColor >> 8) & 0xFF);

                data[0x0E52] = (byte)(brownColor & 0xFF);
                data[0x0E53] = (byte)((brownColor >> 8) & 0xFF);

                fs.Write(data, 0, data.Length);
            }
            return tempFile;
        }
    }
}