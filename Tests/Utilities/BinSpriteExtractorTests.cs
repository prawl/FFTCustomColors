using System;
using System.Drawing;
using System.IO;
using FluentAssertions;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.Utilities
{
    public class BinSpriteExtractorTests : IDisposable
    {
        private readonly BinSpriteExtractor _extractor;
        private readonly string _testBinPath;
        private byte[] _testBinData;

        public BinSpriteExtractorTests()
        {
            _extractor = new BinSpriteExtractor();

            // Create a minimal test .bin file with a simple palette
            // FFT sprite format: First 512 bytes are palette data (256 colors, 2 bytes each)
            _testBinData = new byte[512 + (256 * 40 * 8)]; // Palette + sprite sheet data

            // Set up a test palette with known colors
            // Color format is BGR555: 0bbbbbgggggrrrrr (15-bit color)
            // Color 0 is always transparent, so we start at color 1

            // Set color 1 to pure red (0x001F)
            _testBinData[2] = 0x1F; // Low byte: red=31
            _testBinData[3] = 0x00; // High byte: blue=0, green=0

            // Set color 2 to pure green (0x03E0)
            _testBinData[4] = 0xE0; // Low byte
            _testBinData[5] = 0x03; // High byte: green=31

            // Set color 3 to pure blue (0x7C00)
            _testBinData[6] = 0x00; // Low byte
            _testBinData[7] = 0x7C; // High byte: blue=31

            // Save test data to a temp file
            _testBinPath = Path.GetTempFileName();
            File.WriteAllBytes(_testBinPath, _testBinData);
        }

        [Fact]
        public void ReadPalette_Should_Extract_Correct_Colors_From_Bin_File()
        {
            // Act
            var palette = _extractor.ReadPalette(_testBinData, 0);

            // Assert
            palette.Should().NotBeNull();
            palette.Should().HaveCount(16, "FFT palettes have 16 colors each");

            // Check the colors we set
            // First color is always transparent in FFT
            palette[0].A.Should().Be(0, "First color should be transparent");

            // Pure red (BGR555: 0x001F) should convert to RGB (255, 0, 0) at index 1
            palette[1].R.Should().BeCloseTo(255, 8, "Red component should be ~255");
            palette[1].G.Should().Be(0, "Green component should be 0");
            palette[1].B.Should().Be(0, "Blue component should be 0");

            // Pure green (BGR555: 0x03E0) should convert to RGB (0, 255, 0) at index 2
            palette[2].R.Should().Be(0, "Red component should be 0");
            palette[2].G.Should().BeCloseTo(255, 8, "Green component should be ~255");
            palette[2].B.Should().Be(0, "Blue component should be 0");

            // Pure blue (BGR555: 0x7C00) should convert to RGB (0, 0, 255) at index 3
            palette[3].R.Should().Be(0, "Red component should be 0");
            palette[3].G.Should().Be(0, "Green component should be 0");
            palette[3].B.Should().BeCloseTo(255, 8, "Blue component should be ~255");
        }

        [Fact]
        public void ExtractSprite_Should_Create_64x80_Bitmap_From_Bin_Data()
        {
            // Arrange - Add some sprite data after the palette
            // Sprites in FFT are stored as 32x40 in a 256px wide sheet, displayed as 64x80
            // The sprite sheet uses 4bpp (4 bits per pixel)

            // Fill the first sprite with a simple pattern
            for (int i = 512; i < 512 + 1344; i++)
            {
                _testBinData[i] = 0x22; // Both pixels use color index 2 (green)
            }

            // Act
            var sprite = _extractor.ExtractSprite(_testBinData, 0, 0); // First sprite, first palette

            // Assert
            sprite.Should().NotBeNull();
            sprite.Width.Should().Be(64, "Sprites are scaled to 64 pixels wide");
            sprite.Height.Should().Be(80, "Sprites are scaled to 80 pixels tall");

            // Check that at least one pixel has the color we set (green)
            var pixelColor = sprite.GetPixel(0, 0);
            pixelColor.G.Should().BeGreaterThan(200, "Pixel should use palette color 2 (green)");
        }

        [Fact]
        public void ExtractAllDirections_Should_Return_8_Sprites_For_Character()
        {
            // Arrange - FFT sprites have 8 directions (N, NE, E, SE, S, SW, W, NW)
            // Make the test data array big enough for 8 sprites
            Array.Resize(ref _testBinData, 512 + (8 * 1344));

            // Each character has 8 sprites in sequence
            // Fill 8 sprites with different patterns to verify we get different images
            for (int sprite = 0; sprite < 8; sprite++)
            {
                int offset = 512 + (sprite * 1344);
                byte pattern = (byte)((sprite + 1) << 4 | (sprite + 1)); // Use different color for each sprite

                for (int i = 0; i < 1344; i++)
                {
                    _testBinData[offset + i] = pattern;
                }
            }

            File.WriteAllBytes(_testBinPath, _testBinData);

            // Act
            var sprites = _extractor.ExtractAllDirections(_testBinData, 0, 0); // First character, first palette

            // Assert
            sprites.Should().NotBeNull();
            sprites.Should().HaveCount(8, "FFT characters have 8 directional sprites");

            // All sprites should be 64x80 (scaled up from 32x40)
            foreach (var sprite in sprites)
            {
                sprite.Should().NotBeNull();
                sprite.Width.Should().Be(64);
                sprite.Height.Should().Be(80);
            }

            // Sprites should be different (different patterns)
            sprites.Should().OnlyHaveUniqueItems("Each direction should have a different sprite");
        }

        [Fact]
        public void ExtractSprite_Should_Create_New_Instances_Each_Time()
        {
            // Arrange - Create test data
            for (int i = 512; i < 512 + 1344; i++)
            {
                _testBinData[i] = 0x22; // Use color index 2
            }

            // Act - Extract the same sprite twice
            var sprite1 = _extractor.ExtractSprite(_testBinData, 0, 0);
            var sprite2 = _extractor.ExtractSprite(_testBinData, 0, 0);

            // Assert - Should return different instances (caching disabled to prevent theme switching issues)
            sprite2.Should().NotBeSameAs(sprite1, "Extractor should create new instances to avoid theme caching issues");

            // But the sprites should have the same dimensions
            sprite2.Width.Should().Be(sprite1.Width);
            sprite2.Height.Should().Be(sprite1.Height);

            // Extract with different palette should also be different
            var sprite3 = _extractor.ExtractSprite(_testBinData, 0, 1); // Different palette
            sprite3.Should().NotBeSameAs(sprite1, "Different palette should create new sprite");

            // Extract different sprite should be different
            var sprite4 = _extractor.ExtractSprite(_testBinData, 1, 0); // Different sprite
            sprite4.Should().NotBeSameAs(sprite1, "Different sprite index should create new sprite");
        }

        [Fact]
        public void ExtractCardinalDirections_Should_Return_4_Cardinal_Sprites()
        {
            // Arrange - Make data big enough for 8 sprites
            Array.Resize(ref _testBinData, 512 + (8 * 1344));

            // Fill 8 sprites with different patterns
            for (int sprite = 0; sprite < 8; sprite++)
            {
                int offset = 512 + (sprite * 1344);
                byte pattern = (byte)((sprite + 1) << 4 | (sprite + 1));

                for (int i = 0; i < 1344; i++)
                {
                    _testBinData[offset + i] = pattern;
                }
            }

            // Act - Extract cardinal directions (N, E, S, W)
            var sprites = _extractor.ExtractCardinalDirections(_testBinData, 0, 0);

            // Assert
            sprites.Should().NotBeNull();
            sprites.Should().HaveCount(4, "Should return 4 cardinal direction sprites");

            // All sprites should be 64x80 (scaled up from 32x40)
            foreach (var sprite in sprites)
            {
                sprite.Should().NotBeNull();
                sprite.Width.Should().Be(64);
                sprite.Height.Should().Be(80);
            }

            // Sprites should be different (different patterns)
            sprites.Should().OnlyHaveUniqueItems("Each cardinal direction should be different");
        }

        [Fact]
        public void ExtractCornerDirections_Should_Return_4_Corner_Sprites()
        {
            // Arrange - Make data big enough for 8 sprites
            Array.Resize(ref _testBinData, 512 + (8 * 1344));

            // Fill 8 sprites with different patterns
            for (int sprite = 0; sprite < 8; sprite++)
            {
                int offset = 512 + (sprite * 1344);
                byte pattern = (byte)((sprite + 1) << 4 | (sprite + 1));

                for (int i = 0; i < 1344; i++)
                {
                    _testBinData[offset + i] = pattern;
                }
            }

            // Act - Extract corner directions (NE, SE, SW, NW)
            var sprites = _extractor.ExtractCornerDirections(_testBinData, 0, 0);

            // Assert
            sprites.Should().NotBeNull();
            sprites.Should().HaveCount(4, "Should return 4 corner direction sprites");

            // All sprites should be 64x80 (scaled up from 32x40)
            foreach (var sprite in sprites)
            {
                sprite.Should().NotBeNull();
                sprite.Width.Should().Be(64);
                sprite.Height.Should().Be(80);
            }

            // Sprites should be different (different patterns)
            sprites.Should().OnlyHaveUniqueItems("Each corner direction should be different");
        }

        [Theory]
        [InlineData(DirectionMode.AllEight, 8)]
        [InlineData(DirectionMode.Cardinals, 4)]
        [InlineData(DirectionMode.Corners, 4)]
        public void ExtractDirections_Should_Return_Correct_Number_Based_On_Mode(DirectionMode mode, int expectedCount)
        {
            // Arrange - Make data big enough for 8 sprites
            Array.Resize(ref _testBinData, 512 + (8 * 1344));

            for (int sprite = 0; sprite < 8; sprite++)
            {
                int offset = 512 + (sprite * 1344);
                byte pattern = (byte)((sprite + 1) << 4 | (sprite + 1));

                for (int i = 0; i < 1344; i++)
                {
                    _testBinData[offset + i] = pattern;
                }
            }

            // Act
            var sprites = _extractor.ExtractDirections(_testBinData, 0, 0, mode);

            // Assert
            sprites.Should().NotBeNull();
            sprites.Should().HaveCount(expectedCount, $"{mode} should return {expectedCount} sprites");
        }

        public void Dispose()
        {
            if (File.Exists(_testBinPath))
            {
                File.Delete(_testBinPath);
            }
        }
    }
}