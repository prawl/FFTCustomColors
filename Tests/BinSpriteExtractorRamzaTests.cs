using Xunit;
using FFTColorCustomizer.Utilities;
using FFTColorCustomizer.Configuration.UI;
using FluentAssertions;
using System.IO;
using System;

namespace FFTColorCustomizer.Tests
{
    public class BinSpriteExtractorRamzaTests
    {
        [Fact]
        public void CharacterRowBuilder_Should_Load_Ramza_Chapter1_From_Bin()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), $"BinExtractorTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create the proper directory structure
                var unitPath = Path.Combine(tempDir, "FFTIVC", "data", "enhanced", "fftpack", "unit");
                Directory.CreateDirectory(unitPath);

                // Create a mock bin file for ramuza
                var binPath = Path.Combine(unitPath, "battle_ramuza_spr.bin");
                // Create a minimal valid BIN file structure with enough data
                // Real sprite files are much larger
                var mockData = new byte[512 * 1024]; // 512KB typical sprite size
                File.WriteAllBytes(binPath, mockData);

                var previewManager = new PreviewImageManager(tempDir);
                var rowBuilder = new CharacterRowBuilder(
                    new System.Windows.Forms.TableLayoutPanel(),
                    previewManager,
                    () => false,
                    new System.Collections.Generic.List<System.Windows.Forms.Control>(),
                    new System.Collections.Generic.List<System.Windows.Forms.Control>()
                );

                // Act - Test that TryLoadFromBinFile can find the ramuza sprite
                var method = typeof(CharacterRowBuilder).GetMethod("TryLoadFromBinFile",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var images = method.Invoke(rowBuilder, new object[] { "RamzaChapter1", "original" });

                // Assert
                images.Should().NotBeNull("Should be able to load Ramza Chapter 1 sprites from bin file");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}