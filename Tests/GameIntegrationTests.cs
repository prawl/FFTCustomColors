using Xunit;
using FluentAssertions;
using System;

namespace FFTColorMod.Tests;

public class ModHookIntegrationTests
{
    [Fact]
    public void Mod_ShouldCreateSpriteHook_WhenPatternFound()
    {
        // TLDR: Test that Mod creates sprite hook when pattern is found
        // Arrange
        var mod = new Mod();

        // Act - simulate pattern found
        var patternFound = mod.TestHandlePatternFound(new IntPtr(0x12345678));

        // Assert - hook should be created
        Assert.True(patternFound, "Pattern handler should return true when hook created");
    }

    [Fact]
    public void Mod_ShouldCallSignatureScannerCreateHook_WhenPatternFound()
    {
        // TLDR: Test that Mod calls SignatureScanner.CreateSpriteLoadHook
        // Arrange
        var mod = new Mod();

        // Act - simulate pattern found with hooks
        var hookCreated = mod.TestCreateHookForPattern(new IntPtr(0x12345678));

        // Assert
        Assert.True(hookCreated, "Should create hook when pattern found");
    }

    [Fact]
    public void Mod_ShouldHaveSignatureScannerInitialized_WhenPatternHandled()
    {
        // TLDR: Test that SignatureScanner is available when handling patterns
        // Arrange
        var mod = new Mod();

        // Act & Assert
        Assert.True(mod.IsSignatureScannerReady(), "SignatureScanner should be ready");
    }
}

public class GameIntegrationTests
{
    [Fact]
    public void GameIntegration_ShouldInitializeComponents_WhenCreated()
    {
        // Arrange & Act
        var integration = new GameIntegration();

        // Assert
        integration.MemoryScanner.Should().NotBeNull();
        integration.PaletteDetector.Should().NotBeNull();
        integration.HotkeyManager.Should().NotBeNull();
        integration.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public void GameIntegration_ShouldStartHotkeyMonitoring_WhenInitialized()
    {
        // Arrange
        var integration = new GameIntegration();

        // Act
        integration.StartMonitoring();

        // Assert
        integration.IsMonitoring.Should().BeTrue();
    }

    [Fact]
    public void GameIntegration_ShouldStopHotkeyMonitoring_WhenRequested()
    {
        // Arrange
        var integration = new GameIntegration();
        integration.StartMonitoring();

        // Act
        integration.StopMonitoring();

        // Assert
        integration.IsMonitoring.Should().BeFalse();
    }

    [Fact]
    public void GameIntegration_ShouldApplyColorScheme_WhenHotkeyPressed()
    {
        // Arrange
        var integration = new GameIntegration();

        // Simulate finding a palette in memory
        byte[] fakeMemory = new byte[1024];
        // Place a Chapter 1 palette at offset 100
        fakeMemory[100] = 0xA0;  // Light blue
        fakeMemory[101] = 0x60;
        fakeMemory[102] = 0x40;

        integration.SetTestMemory(fakeMemory);
        integration.StartMonitoring();

        // Act - simulate F2 press for red colors
        integration.ProcessHotkey(0x71); // F2

        // Assert
        integration.LastAppliedScheme.Should().Be("red");
        integration.LastPaletteOffset.Should().Be(100);
    }

    [Fact]
    public void GameIntegration_ShouldApplyChapter4Colors_WhenChapter4PatternFound()
    {
        // Arrange
        var integration = new GameIntegration();

        // Simulate finding Chapter 4 palette in memory (same colors as Chapter 2: 80 40 60)
        byte[] fakeMemory = new byte[1024];
        // Place Chapter 4 palette at offset 400
        fakeMemory[400] = 0x80;  // Purple-blue (Chapter 4/2 colors)
        fakeMemory[401] = 0x40;
        fakeMemory[402] = 0x60;
        fakeMemory[403] = 0x60;  // Medium purple
        fakeMemory[404] = 0x30;
        fakeMemory[405] = 0x50;

        integration.SetTestMemory(fakeMemory);
        integration.StartMonitoring();

        // Act - simulate F2 press for red colors
        integration.ProcessHotkey(0x71); // F2

        // Assert - Should find and transform Chapter 4 palette
        integration.LastAppliedScheme.Should().Be("red");
        integration.LastPaletteOffset.Should().Be(400);

        // Verify colors were actually transformed in memory
        fakeMemory[400].Should().Be(0x30, "B reduced for red");
        fakeMemory[401].Should().Be(0x30, "G reduced for red");
        fakeMemory[402].Should().Be(0x80, "R enhanced for red");
    }

    [Fact]
    public void GameIntegration_ShouldHandleMultipleChapterPatterns_WhenMixedPalettesFound()
    {
        // Arrange
        var integration = new GameIntegration();

        // Simulate memory with multiple chapter palettes
        byte[] fakeMemory = new byte[2048];

        // Chapter 1 palette at offset 200
        fakeMemory[200] = 0xA0;  // Light blue
        fakeMemory[201] = 0x60;
        fakeMemory[202] = 0x40;

        // Chapter 4 palette at offset 800 (same as Chapter 2: 80 40 60)
        fakeMemory[800] = 0x80;  // Purple-blue
        fakeMemory[801] = 0x40;
        fakeMemory[802] = 0x60;

        // Chapter 3 palette at offset 1200
        fakeMemory[1200] = 0x40;  // Burgundy
        fakeMemory[1201] = 0x30;
        fakeMemory[1202] = 0x60;

        integration.SetTestMemory(fakeMemory);
        integration.StartMonitoring();

        // Act - simulate F2 press for red colors
        integration.ProcessHotkey(0x71); // F2

        // Assert - Should find first palette and apply transformation
        integration.LastAppliedScheme.Should().Be("red");
        // Should find first matching palette (Chapter 1 at offset 200)
        integration.LastPaletteOffset.Should().Be(200);

        // Verify Chapter 1 colors were transformed
        fakeMemory[200].Should().Be(0x40, "Chapter 1 B reduced for red");
        fakeMemory[201].Should().Be(0x40, "Chapter 1 G reduced for red");
        fakeMemory[202].Should().Be(0xA0, "Chapter 1 R enhanced for red");
    }
}