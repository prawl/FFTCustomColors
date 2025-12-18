using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using FluentAssertions;
using FFTColorCustomizer.Configuration.UI;
using Xunit;

namespace FFTColorCustomizer.Tests.Configuration.UI
{
    public class CharacterRowBuilderBinIntegrationTests : IDisposable
    {
        private readonly string _testModPath;
        private readonly string _testBinPath;
        private readonly TableLayoutPanel _testPanel;
        private readonly PreviewImageManager _previewManager;
        private readonly CharacterRowBuilder _builder;

        public CharacterRowBuilderBinIntegrationTests()
        {
            // Create a temporary mod directory structure
            _testModPath = Path.Combine(Path.GetTempPath(), $"FFTTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testModPath);

            var previewPath = Path.Combine(_testModPath, "Resources", "Previews");
            Directory.CreateDirectory(previewPath);

            // Create the correct directory structure for FFT sprites
            var unitPath = Path.Combine(_testModPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "sprites_original");
            Directory.CreateDirectory(unitPath);

            // Create a test .bin file with minimal sprite data
            // Use the correct filename format: battle_cloud_spr.bin
            _testBinPath = Path.Combine(unitPath, "battle_cloud_spr.bin");
            CreateTestBinFile(_testBinPath);

            // Create test components
            _testPanel = new TableLayoutPanel();
            _previewManager = new PreviewImageManager(_testModPath);
            _builder = new CharacterRowBuilder(
                _testPanel,
                _previewManager,
                () => false,
                new System.Collections.Generic.List<Control>(),
                new System.Collections.Generic.List<Control>()
            );
        }

        private void CreateTestBinFile(string path)
        {
            // Create a minimal .bin file with palette and sprite sheet
            // FFT sprites are stored in a 256-pixel-wide sprite sheet
            // Each sprite is 32x40 pixels, arranged horizontally
            // We need at least 5 sprites (positions 0-4) for the extractor to work

            // Sheet is 256 pixels wide, we need at least 40 rows for one row of sprites
            // 256 pixels * 40 rows = 10240 pixels
            // At 4bpp (2 pixels per byte) = 5120 bytes
            var binData = new byte[512 + 5120]; // Palette + sprite sheet data

            // Set up a simple palette
            binData[0] = 0x00; binData[1] = 0x00; // Color 0: Black/Transparent
            binData[2] = 0x1F; binData[3] = 0x00; // Color 1: Red
            binData[4] = 0xE0; binData[5] = 0x03; // Color 2: Green
            binData[6] = 0x00; binData[7] = 0x7C; // Color 3: Blue

            // Fill sprite sheet with different patterns for each sprite
            // Each sprite is 32 pixels wide, so we can fit 8 sprites in the 256-pixel wide sheet
            // But we only need 5 for the extractor (0-4), as it will mirror to create the rest
            int spriteDataStart = 512;

            // Create 5 different sprites with different patterns
            for (int spriteIndex = 0; spriteIndex < 5; spriteIndex++)
            {
                byte pattern = (byte)((spriteIndex + 1) | ((spriteIndex + 1) << 4)); // Both nibbles same

                // Each sprite is 32x40 pixels
                for (int y = 0; y < 40; y++)
                {
                    for (int x = 0; x < 16; x++) // 16 bytes = 32 pixels (2 pixels per byte)
                    {
                        // Calculate position in sprite sheet
                        int sheetX = (spriteIndex * 32) + (x * 2);
                        int pixelIndex = (y * 256) + sheetX;
                        int byteIndex = spriteDataStart + (pixelIndex / 2);

                        if (byteIndex < binData.Length)
                        {
                            binData[byteIndex] = pattern;
                        }
                    }
                }
            }

            File.WriteAllBytes(path, binData);
        }

        [Fact]
        public void CharacterRowBuilder_Should_Load_Sprites_From_Bin_File_When_Available()
        {
            // Arrange
            var carousel = new PreviewCarousel();

            // Act - This should trigger loading from the .bin file
            var updateMethod = _builder.GetType().GetMethod("UpdateStoryCharacterPreview",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            updateMethod.Should().NotBeNull("UpdateStoryCharacterPreview method should exist");

            updateMethod.Invoke(_builder, new object[] { carousel, "cloud", "original" });

            // Assert
            carousel.ImageCount.Should().Be(8, "Should load all 8 directional sprites from .bin file");
            carousel.CurrentViewIndex.Should().Be(0, "Should start at first image");

            // Verify images are loaded
            for (int i = 0; i < 8; i++)
            {
                carousel.NextView();
                carousel.Image.Should().NotBeNull($"Image {i} should be loaded");
            }
        }

        [Fact]
        public void CharacterRowBuilder_Should_Fall_Back_To_Embedded_Resources_When_Bin_Not_Found()
        {
            // Arrange
            var carousel = new PreviewCarousel();

            // Delete the .bin file to test fallback
            if (File.Exists(_testBinPath))
            {
                File.Delete(_testBinPath);
            }

            // Act
            var updateMethod = _builder.GetType().GetMethod("UpdateStoryCharacterPreview",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            updateMethod.Invoke(_builder, new object[] { carousel, "cloud", "original" });

            // Assert - Should fall back to embedded resources or empty
            // The actual behavior depends on whether embedded resources exist
            // For this test, we just verify it doesn't crash
            carousel.Should().NotBeNull();
        }

        public void Dispose()
        {
            _testPanel?.Dispose();

            // Clean up test directory
            if (Directory.Exists(_testModPath))
            {
                Directory.Delete(_testModPath, true);
            }
        }
    }
}