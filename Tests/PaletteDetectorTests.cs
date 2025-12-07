using Xunit;
using FluentAssertions;

namespace FFTColorMod.Tests;

public class PaletteDetectorTests
{
    [Fact]
    public void DetectBrownColor_ShouldReturnTrue_WhenColorIsBrown()
    {
        // Arrange
        var detector = new PaletteDetector();
        byte r = 74, g = 44, b = 23; // Brown color (0x4A, 0x2C, 0x17)

        // Act
        var result = detector.IsBrownColor(r, g, b);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void DetectBrownColor_ShouldReturnFalse_WhenColorIsBlue()
    {
        // Arrange
        var detector = new PaletteDetector();
        byte r = 26, g = 44, b = 127; // Blue color

        // Act
        var result = detector.IsBrownColor(r, g, b);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void FindPalette_ShouldFindRamzaPalette_WhenMemoryContainsBrownColors()
    {
        // Arrange
        var detector = new PaletteDetector();

        // Simulate memory with Ramza's palette embedded (BGR format)
        // This simulates finding Ramza's brown tunic colors in memory
        byte[] memoryData = new byte[]
        {
            0xFF, 0xFF, 0xFF,  // Random data before palette
            0x00, 0x00, 0x00,  // Random data
            // Start of Ramza's palette (BGR format)
            0x17, 0x2C, 0x4A,  // Dark brown tunic
            0x21, 0x3A, 0x5A,  // Medium brown tunic
            0x31, 0x4A, 0x6B,  // Light brown tunic
            0x08, 0x1C, 0x31,  // Very dark brown (shadows)
            // Some other colors mixed in
            0x29, 0x31, 0x42,  // Dark leather
            0x39, 0x42, 0x52,  // Medium leather
            0xFF, 0xFF, 0xFF,  // Random data after
        };

        // Act
        var paletteOffset = detector.FindPalette(memoryData);

        // Assert
        paletteOffset.Should().NotBe(-1, "should find the palette in memory");
        paletteOffset.Should().Be(6, "palette starts at offset 6 where first brown color begins");
    }

    [Fact]
    public void FindPalette_ShouldReturnNegativeOne_WhenNoPaletteFound()
    {
        // Arrange
        var detector = new PaletteDetector();

        // Memory with no brown colors
        byte[] memoryData = new byte[]
        {
            0xFF, 0xFF, 0xFF,
            0x00, 0x00, 0x00,
            0x12, 0x34, 0x56,
            0x78, 0x9A, 0xBC,
        };

        // Act
        var paletteOffset = detector.FindPalette(memoryData);

        // Assert
        paletteOffset.Should().Be(-1, "no palette found in memory");
    }

    [Fact]
    public void FindAllPalettes_ShouldFindMultiplePalettes_WhenMemoryContainsMultipleRamzas()
    {
        // Arrange
        var detector = new PaletteDetector();

        // Simulate memory with two Ramza palettes
        byte[] memoryData = new byte[]
        {
            0xFF, 0xFF, 0xFF,  // Random data (indices 0,1,2)
            // First Ramza palette at offset 3
            0x17, 0x2C, 0x4A,  // Dark brown tunic (indices 3,4,5)
            0x21, 0x3A, 0x5A,  // Medium brown (indices 6,7,8)
            // Gap
            0x00, 0x00, 0x00,  // Gap (indices 9,10,11)
            // Second Ramza palette at offset 12
            0x17, 0x2C, 0x4A,  // Dark brown tunic (indices 12,13,14)
            0x31, 0x4A, 0x6B,  // Light brown (indices 15,16,17)
        };

        // Act
        var paletteOffsets = detector.FindAllPalettes(memoryData);

        // Assert
        paletteOffsets.Should().HaveCount(2, "found two palettes");
        paletteOffsets[0].Should().Be(3, "first palette at offset 3");
        paletteOffsets[1].Should().Be(12, "second palette at offset 12");
    }


    [Fact]
    public void ReplacePaletteColors_RedScheme_ShouldReplaceBrownWithRed()
    {
        // TLDR: Test brown → red replacement
        var detector = new PaletteDetector();
        byte[] data = new byte[] { 0x17, 0x2C, 0x4A, 0x21, 0x3A, 0x5A }; // Original brown colors

        detector.ReplacePaletteColors(data, 0, "red");

        data[0].Should().Be(0x1A); // B: dark red blue component
        data[1].Should().Be(0x2C); // G: keep similar
        data[2].Should().Be(0x7F); // R: red (127)
        data[3].Should().Be(0x2A); // B: medium red blue component
        data[4].Should().Be(0x3A); // G: keep similar
        data[5].Should().Be(0x9F); // R: brighter red (159)
    }

    [Fact]
    public void ReplacePaletteColors_BlueScheme_ShouldReplaceBrownWithBlue()
    {
        // TLDR: Test brown → blue replacement for default chapter
        var detector = new PaletteDetector();
        byte[] data = new byte[] { 0x17, 0x2C, 0x4A }; // Original brown color

        detector.ReplacePaletteColors(data, 0, "blue");

        data[0].Should().Be(0x7F); // B: enhanced blue
        data[1].Should().Be(0x2C); // G: keep similar
        data[2].Should().Be(0x1A); // R: reduced
    }

    [Fact]
    public void ReplacePaletteColors_GreenScheme_ShouldReplaceBrownWithGreen()
    {
        // TLDR: Test brown → green replacement for default chapter
        var detector = new PaletteDetector();
        byte[] data = new byte[] { 0x17, 0x2C, 0x4A }; // Original brown color

        detector.ReplacePaletteColors(data, 0, "green");

        data[0].Should().Be(0x1A); // B: reduced
        data[1].Should().Be(0x7F); // G: enhanced green
        data[2].Should().Be(0x2C); // R: keep similar
    }

    [Fact]
    public void ReplacePaletteColors_PurpleScheme_ShouldReplaceBrownWithPurple()
    {
        // TLDR: Test brown → purple replacement for default chapter
        var detector = new PaletteDetector();
        byte[] data = new byte[] { 0x17, 0x2C, 0x4A }; // Original brown color

        detector.ReplacePaletteColors(data, 0, "purple");

        data[0].Should().Be(0x7F); // B: enhanced for purple
        data[1].Should().Be(0x2C); // G: keep similar
        data[2].Should().Be(0x7F); // R: enhanced for purple
    }
}