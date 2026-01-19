using Xunit;
using FFTColorCustomizer.Services;
using System;
using System.IO;

namespace FFTColorCustomizer.Tests.Services
{
    public class NxdPatcherTests
    {
        private readonly NxdPatcher _patcher;

        public NxdPatcherTests()
        {
            _patcher = new NxdPatcher();
        }

        [Theory]
        [InlineData(1, 0, 0x379)]
        [InlineData(1, 1, 0x3A9)]
        [InlineData(2, 0, 0x439)]
        [InlineData(3, 0, 0x4F9)]
        [InlineData(254, 0, 0x5B9)]
        [InlineData(255, 0, 0x619)]
        public void GetClutDataOffset_ReturnsCorrectOffset(int key, int key2, int expectedOffset)
        {
            // Act
            var offset = _patcher.GetClutDataOffset(key, key2);

            // Assert
            Assert.NotNull(offset);
            Assert.Equal(expectedOffset, offset.Value);
        }

        [Fact]
        public void GetClutDataOffset_WithInvalidKey_ReturnsNull()
        {
            // Act
            var offset = _patcher.GetClutDataOffset(999, 0);

            // Assert
            Assert.Null(offset);
        }

        [Fact]
        public void ClutBytesToJson_ConvertsCorrectly()
        {
            // Arrange
            var bytes = new byte[] { 255, 0, 0, 0, 255, 0 }; // Red, Green

            // Act
            var json = _patcher.ClutBytesToJson(bytes);

            // Assert
            Assert.Equal("[255,0,0,0,255,0]", json);
        }

        [Fact]
        public void PatchSingleEntry_UpdatesCorrectOffset()
        {
            // Arrange
            var baseDir = Directory.GetCurrentDirectory();
            var templateNxdPath = Path.Combine(baseDir, "..", "..", "..", "..", "ColorMod", "Data", "nxd", "charclut.nxd");
            templateNxdPath = Path.GetFullPath(templateNxdPath);

            if (!File.Exists(templateNxdPath))
            {
                return;
            }

            var testNxdPath = Path.Combine(Path.GetTempPath(), $"charclut_test_{Guid.NewGuid()}.nxd");
            File.Copy(templateNxdPath, testNxdPath);

            try
            {
                // Create a test CLUT with all red (255,0,0 repeated 16 times)
                var redClut = new int[48];
                for (int i = 0; i < 16; i++)
                {
                    redClut[i * 3] = 255;     // R
                    redClut[i * 3 + 1] = 0;   // G
                    redClut[i * 3 + 2] = 0;   // B
                }
                var redClutJson = System.Text.Json.JsonSerializer.Serialize(redClut);

                // Act
                var result = _patcher.PatchSingleEntry(testNxdPath, 1, 0, redClutJson);

                // Assert
                Assert.True(result);

                var patchedBytes = File.ReadAllBytes(testNxdPath);
                var offset = 0x379;

                // All 16 colors should now be red (255,0,0)
                for (int i = 0; i < 16; i++)
                {
                    Assert.Equal(255, patchedBytes[offset + i * 3]);     // R
                    Assert.Equal(0, patchedBytes[offset + i * 3 + 1]);   // G
                    Assert.Equal(0, patchedBytes[offset + i * 3 + 2]);   // B
                }
            }
            finally
            {
                if (File.Exists(testNxdPath))
                    File.Delete(testNxdPath);
            }
        }
    }
}
