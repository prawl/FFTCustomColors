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
    public void DetectChapterOutfit_ShouldReturnChapter1_WhenLightBlueTunicDetected()
    {
        // Arrange
        var detector = new PaletteDetector();

        // Chapter 1 Ramza's light blue tunic colors (BGR format)
        byte[] memoryData = new byte[]
        {
            0xFF, 0xFF, 0xFF,  // Random data
            0xA0, 0x60, 0x40,  // Light blue (BGR)
            0x80, 0x50, 0x30,  // Medium blue (BGR)
            0x60, 0x40, 0x20,  // Dark blue shadows (BGR)
        };

        // Act
        var chapter = detector.DetectChapterOutfit(memoryData, 3);

        // Assert
        chapter.Should().Be(1, "Chapter 1 outfit has light blue tunic");
    }

    [Fact]
    public void DetectChapterOutfit_ShouldReturnChapter2_WhenPurpleBlueTunicDetected()
    {
        // Arrange
        var detector = new PaletteDetector();

        // Chapter 2 Ramza's purple-blue tunic colors (BGR format)
        byte[] memoryData = new byte[]
        {
            0xFF, 0xFF, 0xFF,  // Random data
            0x80, 0x40, 0x60,  // Purple-blue (BGR)
            0x60, 0x30, 0x50,  // Medium purple (BGR)
            0x40, 0x20, 0x40,  // Dark purple shadows (BGR)
        };

        // Act
        var chapter = detector.DetectChapterOutfit(memoryData, 3);

        // Assert
        chapter.Should().Be(2, "Chapter 2 outfit has purple-blue tunic");
    }

    [Fact]
    public void DetectChapterOutfit_ShouldReturnChapter3_WhenBurgundyOutfitDetected()
    {
        // Arrange
        var detector = new PaletteDetector();

        // Chapter 3 Ramza's burgundy outfit colors (BGR format)
        byte[] memoryData = new byte[]
        {
            0xFF, 0xFF, 0xFF,  // Random data
            0x40, 0x30, 0x60,  // Burgundy (BGR)
            0x30, 0x25, 0x50,  // Medium burgundy (BGR)
            0x20, 0x18, 0x40,  // Dark burgundy (BGR)
        };

        // Act
        var chapter = detector.DetectChapterOutfit(memoryData, 3);

        // Assert
        chapter.Should().Be(3, "Chapter 3 outfit has burgundy tunic");
    }

    [Fact]
    public void DetectChapterOutfit_ShouldReturnChapter4_WhenChapter4ColorsDetected()
    {
        // Arrange
        var detector = new PaletteDetector();

        // Chapter 4 Ramza uses same colors as Chapter 2 but should return 4
        // This test will help us figure out how to distinguish Chapter 4 from Chapter 2
        // For now, we'll test with Chapter 2 colors but expect Chapter 4 result
        byte[] memoryData = new byte[]
        {
            0xFF, 0xFF, 0xFF,  // Random data
            0x80, 0x40, 0x60,  // Same purple-blue as Chapter 2 (BGR)
            0x60, 0x30, 0x50,  // Medium purple (BGR)
            0x40, 0x20, 0x40,  // Dark purple shadows (BGR)
        };

        // Act
        var chapter = detector.DetectChapterOutfit(memoryData, 3, preferChapter4: true);

        // Assert
        // This test verifies Chapter 4 detection with preferChapter4 = true
        chapter.Should().Be(4, "Chapter 4 outfit should be detected when preferChapter4 is true");
    }

    [Fact]
    public void ReplacePaletteColors_ShouldReplaceChapter1BlueWithRed_WhenRedSchemeSelected()
    {
        // Arrange
        var detector = new PaletteDetector();

        // Chapter 1 blue tunic palette
        byte[] memoryData = new byte[]
        {
            0xFF, 0xFF, 0xFF,  // Random data
            0xA0, 0x60, 0x40,  // Light blue (BGR)
            0x80, 0x50, 0x30,  // Medium blue (BGR)
            0x60, 0x40, 0x20,  // Dark blue shadows (BGR)
        };

        // Act
        detector.ReplacePaletteColors(memoryData, 3, "red", 1);

        // Assert - Blue to Red transformation
        memoryData[3].Should().Be(0x40, "B reduced for red");
        memoryData[4].Should().Be(0x40, "G reduced for red");
        memoryData[5].Should().Be(0xA0, "R enhanced for red");

        memoryData[6].Should().Be(0x30, "B reduced for red");
        memoryData[7].Should().Be(0x30, "G reduced for red");
        memoryData[8].Should().Be(0x80, "R enhanced for red");
    }

    [Fact]
    public void ReplacePaletteColors_ShouldReplaceChapter2PurpleWithRed_WhenRedSchemeSelected()
    {
        // Arrange
        var detector = new PaletteDetector();

        // Chapter 2 purple-blue tunic palette
        byte[] memoryData = new byte[]
        {
            0xFF, 0xFF, 0xFF,  // Random data
            0x80, 0x40, 0x60,  // Purple-blue (BGR)
            0x60, 0x30, 0x50,  // Medium purple (BGR)
            0x40, 0x20, 0x40,  // Dark purple shadows (BGR)
        };

        // Act
        detector.ReplacePaletteColors(memoryData, 3, "red", 2);

        // Assert - Purple to Red transformation
        memoryData[3].Should().Be(0x30, "B reduced for red");
        memoryData[4].Should().Be(0x30, "G reduced for red");
        memoryData[5].Should().Be(0x80, "R enhanced for red");

        memoryData[6].Should().Be(0x25, "B reduced for red");
        memoryData[7].Should().Be(0x25, "G reduced for red");
        memoryData[8].Should().Be(0x70, "R enhanced for red");
    }

    [Fact]
    public void ReplacePaletteColors_ShouldReplaceChapter3BurgundyWithRed_WhenRedSchemeSelected()
    {
        // Arrange
        var detector = new PaletteDetector();

        // Chapter 3 burgundy outfit palette
        byte[] memoryData = new byte[]
        {
            0xFF, 0xFF, 0xFF,  // Random data
            0x40, 0x30, 0x60,  // Burgundy (BGR)
            0x30, 0x25, 0x50,  // Medium burgundy (BGR)
            0x20, 0x18, 0x40,  // Dark burgundy (BGR)
        };

        // Act
        detector.ReplacePaletteColors(memoryData, 3, "red", 3);

        // Assert - Burgundy to Red transformation
        memoryData[3].Should().Be(0x1A, "B reduced for red");
        memoryData[4].Should().Be(0x2C, "G adjusted for red");
        memoryData[5].Should().Be(0x7F, "R enhanced for red");

        memoryData[6].Should().Be(0x15, "B reduced for red");
        memoryData[7].Should().Be(0x20, "G adjusted for red");
        memoryData[8].Should().Be(0x65, "R enhanced for red");
    }

    [Fact]
    public void ReplacePaletteColors_ShouldReplaceChapter4PurpleWithRed_WhenRedSchemeSelected()
    {
        // Arrange
        var detector = new PaletteDetector();

        // Chapter 4 uses same purple colors as Chapter 2 (BGR format)
        byte[] memoryData = new byte[]
        {
            0xFF, 0xFF, 0xFF,  // Random data
            0x80, 0x40, 0x60,  // Purple-blue (BGR) - same as Chapter 2
            0x60, 0x30, 0x50,  // Medium purple (BGR)
            0x40, 0x20, 0x40,  // Dark purple shadows (BGR)
        };

        // Act
        detector.ReplacePaletteColors(memoryData, 3, "red", 4);

        // Assert - Chapter 4 Purple to Red transformation (should be same as Chapter 2)
        memoryData[3].Should().Be(0x30, "B reduced for red");
        memoryData[4].Should().Be(0x30, "G reduced for red");
        memoryData[5].Should().Be(0x80, "R enhanced for red");

        memoryData[6].Should().Be(0x25, "B reduced for red");
        memoryData[7].Should().Be(0x25, "G reduced for red");
        memoryData[8].Should().Be(0x70, "R enhanced for red");
    }

    [Fact]
    public void ReplacePaletteColors_BlueScheme_ShouldReplaceBrownWithBlue()
    {
        // TLDR: Test brown → blue replacement for default chapter
        var detector = new PaletteDetector();
        byte[] data = new byte[] { 0x17, 0x2C, 0x4A }; // Original brown color

        detector.ReplacePaletteColors(data, 0, "blue", 0);

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

        detector.ReplacePaletteColors(data, 0, "green", 0);

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

        detector.ReplacePaletteColors(data, 0, "purple", 0);

        data[0].Should().Be(0x7F); // B: enhanced for purple
        data[1].Should().Be(0x2C); // G: keep similar
        data[2].Should().Be(0x7F); // R: enhanced for purple
    }
}