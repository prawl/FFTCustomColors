using Xunit;
using FluentAssertions;

namespace FFTColorMod.Tests;

public class PaletteDetectorOptimizationTests
{
    [Fact]
    public void FindPalette_ShouldOnlySearchFirst288Bytes()
    {
        // TLDR: Test that FindPalette only searches within palette data (first 288 bytes)
        var detector = new PaletteDetector();

        // Create a large array with palette marker AFTER byte 288
        var data = new byte[1000];

        // Place the brown color marker at position 300 (outside palette range)
        data[300] = 0x17;
        data[301] = 0x2C;
        data[302] = 0x4A;

        // Should NOT find the palette since it's outside the 288-byte range
        int result = detector.FindPalette(data);

        result.Should().Be(-1, "palette data is only in first 288 bytes");
    }

    [Fact]
    public void FindPalette_ShouldFindPaletteWithinFirst288Bytes()
    {
        // TLDR: Test that FindPalette still finds palettes within the valid range
        var detector = new PaletteDetector();

        // Create array with palette marker WITHIN first 288 bytes
        var data = new byte[1000];

        // Place the brown color marker at position 100 (within palette range)
        data[100] = 0x17;
        data[101] = 0x2C;
        data[102] = 0x4A;

        // Should find the palette since it's within the 288-byte range
        int result = detector.FindPalette(data);

        result.Should().Be(100);
    }

    [Fact]
    public void FindAllPalettes_ShouldOnlySearchFirst288Bytes()
    {
        // TLDR: Test that FindAllPalettes only searches within palette data
        var detector = new PaletteDetector();

        // Create a large array with palette markers
        var data = new byte[1000];

        // Place one marker within palette range
        data[50] = 0x17;
        data[51] = 0x2C;
        data[52] = 0x4A;

        // Place another marker outside palette range
        data[300] = 0x17;
        data[301] = 0x2C;
        data[302] = 0x4A;

        // Should only find the first one
        var results = detector.FindAllPalettes(data);

        results.Should().HaveCount(1);
        results.Should().Contain(50);
        results.Should().NotContain(300);
    }
}