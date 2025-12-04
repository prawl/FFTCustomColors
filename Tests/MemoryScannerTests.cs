using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;

namespace FFTColorMod.Tests;

public class MemoryScannerTests
{
    [Fact]
    public void ScanForPalettes_ShouldFindPaletteInMemory_WhenPatternExists()
    {
        // Arrange
        var scanner = new MemoryScanner();
        var detector = new PaletteDetector();

        // Simulate a memory region with Ramza's Chapter 1 palette
        byte[] simulatedMemory = new byte[1024];
        // Place palette at offset 500
        simulatedMemory[500] = 0xA0;  // Light blue (Chapter 1)
        simulatedMemory[501] = 0x60;
        simulatedMemory[502] = 0x40;
        simulatedMemory[503] = 0x80;  // Medium blue
        simulatedMemory[504] = 0x50;
        simulatedMemory[505] = 0x30;

        // Act
        var foundAddresses = scanner.ScanForPalettes(simulatedMemory, detector);

        // Assert
        foundAddresses.Should().NotBeEmpty();
        foundAddresses.Should().Contain(500, "palette was placed at offset 500");
    }

    [Fact]
    public void ScanForPalettes_ShouldReturnEmpty_WhenNoPatternsFound()
    {
        // Arrange
        var scanner = new MemoryScanner();
        var detector = new PaletteDetector();

        // Memory with no palette patterns
        byte[] simulatedMemory = new byte[1024];
        for (int i = 0; i < simulatedMemory.Length; i++)
        {
            simulatedMemory[i] = 0xFF; // Fill with non-palette data
        }

        // Act
        var foundAddresses = scanner.ScanForPalettes(simulatedMemory, detector);

        // Assert
        foundAddresses.Should().BeEmpty();
    }

    [Fact]
    public void ScanForPalettes_ShouldFindMultiplePalettes_WhenChapter2PatternExists()
    {
        // Arrange
        var scanner = new MemoryScanner();
        var detector = new PaletteDetector();

        byte[] simulatedMemory = new byte[2048];

        // Place Chapter 1 palette at offset 100
        simulatedMemory[100] = 0xA0;
        simulatedMemory[101] = 0x60;
        simulatedMemory[102] = 0x40;

        // Place Chapter 2 palette at offset 500
        simulatedMemory[500] = 0x80;
        simulatedMemory[501] = 0x40;
        simulatedMemory[502] = 0x60;

        // Act
        var foundAddresses = scanner.ScanForPalettes(simulatedMemory, detector);

        // Assert
        foundAddresses.Should().HaveCount(2);
        foundAddresses.Should().Contain(100, "Chapter 1 palette was placed at offset 100");
        foundAddresses.Should().Contain(500, "Chapter 2 palette was placed at offset 500");
    }

    [Fact]
    public void ApplyColorScheme_ShouldModifyMemory_WhenPaletteFound()
    {
        // Arrange
        var scanner = new MemoryScanner();
        var detector = new PaletteDetector();

        byte[] simulatedMemory = new byte[1024];
        // Place Chapter 1 palette
        simulatedMemory[100] = 0xA0;  // Light blue
        simulatedMemory[101] = 0x60;
        simulatedMemory[102] = 0x40;
        simulatedMemory[103] = 0x80;  // Medium blue
        simulatedMemory[104] = 0x50;
        simulatedMemory[105] = 0x30;

        // Act
        scanner.ApplyColorScheme(simulatedMemory, 100, "red", detector, 1);

        // Assert - Should be transformed to red
        simulatedMemory[100].Should().Be(0x40, "blue reduced for red");
        simulatedMemory[101].Should().Be(0x40, "green reduced for red");
        simulatedMemory[102].Should().Be(0xA0, "red enhanced");
        simulatedMemory[103].Should().Be(0x30, "blue reduced for red");
        simulatedMemory[104].Should().Be(0x30, "green reduced for red");
        simulatedMemory[105].Should().Be(0x80, "red enhanced");
    }

    [Fact]
    public void ScanForPalettes_ShouldFindChapter4Palette_WhenPatternExists()
    {
        // Arrange
        var scanner = new MemoryScanner();
        var detector = new PaletteDetector();

        // Simulate memory with Chapter 4 palette (same colors as Chapter 2: 80 40 60)
        byte[] simulatedMemory = new byte[1024];
        // Place Chapter 4 palette at offset 200
        simulatedMemory[200] = 0x80;  // Purple-blue (same as Chapter 2)
        simulatedMemory[201] = 0x40;
        simulatedMemory[202] = 0x60;
        simulatedMemory[203] = 0x60;  // Medium purple
        simulatedMemory[204] = 0x30;
        simulatedMemory[205] = 0x50;

        // Act
        var foundAddresses = scanner.ScanForPalettes(simulatedMemory, detector);

        // Assert - Should find Chapter 4 pattern (same as Chapter 2 pattern)
        foundAddresses.Should().NotBeEmpty();
        foundAddresses.Should().Contain(200, "Chapter 4 palette was placed at offset 200");
    }

    [Fact]
    public void ApplyColorScheme_ShouldModifyChapter4Colors_WhenChapter4Detected()
    {
        // Arrange
        var scanner = new MemoryScanner();
        var detector = new PaletteDetector();

        byte[] simulatedMemory = new byte[1024];
        // Place Chapter 4 palette (same pattern as Chapter 2: 80 40 60)
        simulatedMemory[300] = 0x80;  // Purple-blue
        simulatedMemory[301] = 0x40;
        simulatedMemory[302] = 0x60;
        simulatedMemory[303] = 0x60;  // Medium purple
        simulatedMemory[304] = 0x30;
        simulatedMemory[305] = 0x50;

        // Act - Apply red color scheme for Chapter 4
        scanner.ApplyColorScheme(simulatedMemory, 300, "red", detector, 4);

        // Assert - Should be transformed to red (same as Chapter 2 transformation)
        simulatedMemory[300].Should().Be(0x30, "B reduced for red");
        simulatedMemory[301].Should().Be(0x30, "G reduced for red");
        simulatedMemory[302].Should().Be(0x80, "R enhanced for red");
        simulatedMemory[303].Should().Be(0x25, "B reduced for red");
        simulatedMemory[304].Should().Be(0x25, "G reduced for red");
        simulatedMemory[305].Should().Be(0x70, "R enhanced for red");
    }

    [Fact]
    public void HotkeyManager_ShouldTrackColorScheme_WhenF1OrF2Pressed()
    {
        // Arrange
        var hotkeyManager = new HotkeyManager();

        // Act & Assert - F1 for original colors
        hotkeyManager.ProcessHotkey(0x70); // F1 key code
        hotkeyManager.CurrentScheme.Should().Be("original");

        // Act & Assert - F2 for red colors
        hotkeyManager.ProcessHotkey(0x71); // F2 key code
        hotkeyManager.CurrentScheme.Should().Be("red");

        // Act & Assert - F1 again to go back to original
        hotkeyManager.ProcessHotkey(0x70);
        hotkeyManager.CurrentScheme.Should().Be("original");
    }

    [Fact]
    public void ScanForAllPalettesInMemoryRegions_ShouldFindPalettesAcrossMultipleRegions_WhenPatternsExistInDifferentRegions()
    {
        // Arrange
        var scanner = new MemoryScanner();
        var detector = new PaletteDetector();

        // Simulate multiple memory regions like we'd get from scanning different memory offsets
        var memoryRegions = new List<(byte[] data, long baseOffset)>
        {
            // Cached palette region (base + 0x1000000)
            (new byte[1024], 0x1000000),
            // Graphics memory region (base + 0x10000000)
            (new byte[1024], 0x10000000),
            // Active rendering region (base + 0x20000000)
            (new byte[1024], 0x20000000)
        };

        // Place same Chapter 2 palette in multiple regions (simulating cached vs active copies)
        foreach (var region in memoryRegions)
        {
            region.data[100] = 0x80;  // Chapter 2 purple pattern
            region.data[101] = 0x40;
            region.data[102] = 0x60;
        }

        // Act
        var allFoundPalettes = scanner.ScanForAllPalettesInMemoryRegions(memoryRegions, detector);

        // Assert
        allFoundPalettes.Should().HaveCount(3, "should find palette in all 3 memory regions");
        allFoundPalettes[0].memoryAddress.Should().Be(0x1000064, "first region base + offset 100");
        allFoundPalettes[1].memoryAddress.Should().Be(0x10000064, "second region base + offset 100");
        allFoundPalettes[2].memoryAddress.Should().Be(0x20000064, "third region base + offset 100");
        allFoundPalettes.Should().OnlyContain(p => p.chapter == 2, "all should be detected as Chapter 2");
    }
}