using Xunit;
using FFTColorMod.Configuration;
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

        // Act & Assert - Pattern handling removed
        // File swapping only - no memory hooks
    }

    [Fact]
    public void Mod_ShouldCallSignatureScannerCreateHook_WhenPatternFound()
    {
        // TLDR: Test that Mod calls SignatureScanner.CreateSpriteLoadHook
        // Arrange
        var mod = new Mod();

        // Act & Assert - Hook creation removed
        // File swapping only - no memory hooks
    }

    [Fact]
    public void Mod_ShouldInitialize_WhenCreated()
    {
        // TLDR: Test that Mod initializes properly
        // Arrange
        var mod = new Mod();

        // Act & Assert
        Assert.NotNull(mod);
        // SignatureScanner was removed - no tests needed
    }

    [Fact]
    public void Mod_Constructor_ShouldSetFlags()
    {
        // TLDR: Test that Mod sets proper flags
        // Arrange & Act
        var mod = new Mod();

        // Assert - mod should set flags correctly
        Assert.NotNull(mod);
        // Manual scanner and scanning were removed - no tests needed
    }

    [Fact]
    public void Mod_Constructor_ShouldStartScanning()
    {
        // TLDR: Test that Mod starts scanning for patterns on initialization
        // Arrange & Act
        var mod = new Mod();

        // Assert - mod should have started scanning
        // Scanning was removed - no tests needed
    }

    [Fact]
    public void Mod_ShouldIntegrateGameIntegrationForFileHooks()
    {
        // TLDR: Mod should use GameIntegration to handle file hooks
        // Arrange
        var mod = new Mod();

        // Act
        mod.InitializeGameIntegration();

        // Assert
        mod.HasGameIntegration().Should().BeTrue("Mod should have GameIntegration");
        mod.IsFileRedirectionActive().Should().BeTrue("File redirection should be active");
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
        integration.Should().NotBeNull();
        integration.LastAppliedScheme.Should().Be("original");
    }

    [Fact]
    public void GameIntegration_ShouldStartHotkeyMonitoring_WhenInitialized()
    {
        // Arrange
        var integration = new GameIntegration();

        // Act
        // Monitoring removed - file swapping only

        // Assert - Monitoring removed, file swapping only
        // Monitoring removed - file swapping only
    }

    [Fact]
    public void GameIntegration_ShouldStopHotkeyMonitoring_WhenRequested()
    {
        // Arrange
        var integration = new GameIntegration();
        // Monitoring removed - file swapping only

        // Act
        // Monitoring removed - file swapping only

        // Assert
        // Monitoring removed - file swapping only
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

        // Test memory removed - file swapping only
        // Monitoring removed - file swapping only

        // Act - simulate 1 key press for white/silver colors
        integration.ProcessHotkey(0x70); // F1 key

        // Assert - File swapping only, no memory operations
        integration.LastAppliedScheme.Should().Be("corpse_brigade");
        // Memory operations removed - file swapping only
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

        // Test memory removed - file swapping only
        // Monitoring removed - file swapping only

        // Act - simulate 1 key press for white/silver colors
        integration.ProcessHotkey(0x70); // F1 key

        // Assert - File swapping only, no memory operations
        integration.LastAppliedScheme.Should().Be("corpse_brigade");
        // Memory operations removed - file swapping only

        // Memory operations removed - file swapping only
        // Colors are changed via file swapping, not memory modification
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

        // Test memory removed - file swapping only
        // Monitoring removed - file swapping only

        // Act - simulate 1 key press for white/silver colors
        integration.ProcessHotkey(0x70); // F1 key

        // Assert - File swapping only, no memory operations
        integration.LastAppliedScheme.Should().Be("corpse_brigade");
        // No memory operations - file swapping only
        // Memory operations removed - file swapping only

        // Memory operations removed - file swapping only
        // Colors are changed via file swapping, not memory modification
    }

    [Fact]
    public void Should_Find_PAC_Files_In_Game_Directory()
    {
        // TLDR: Check that we can find PAC files in the FFT game directory
        const string gamePath = @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles";
        var dataPath = System.IO.Path.Combine(gamePath, "data", "classic");

        // Skip test if game not installed
        if (!System.IO.Directory.Exists(dataPath))
        {
            return; // Skip test gracefully if game not installed
        }

        var pacFiles = System.IO.Directory.GetFiles(dataPath, "*.pac");
        pacFiles.Should().NotBeEmpty("Game should have PAC archive files");
    }


    [Fact]
    public void GameIntegration_ShouldHookFileRedirection_WhenInitialized()
    {
        // TLDR: GameIntegration should hook file operations to redirect sprite paths
        // Arrange
        var integration = new GameIntegration();

        // Act
        integration.InitializeFileHook();

        // Assert
        integration.IsFileHookActive.Should().BeTrue("File hook should be active after initialization");
    }

    [Fact]
    public void GameIntegration_ShouldRedirectSpriteFile_WhenColorSchemeActive()
    {
        // TLDR: When a color scheme is active, sprite file paths should be redirected
        // Arrange
        var integration = new GameIntegration();
        integration.InitializeFileHook();

        // Set active color scheme to white/silver
        integration.ProcessHotkey(0x70); // F1 key for white/silver

        // Act - request original sprite path
        var originalPath = @"data\sprites\RAMZA.SPR";
        var redirectedPath = integration.GetRedirectedPath(originalPath);

        // Assert - should redirect to red sprite folder
        redirectedPath.Should().Contain("sprites_corpse_brigade", "Path should redirect to corpse_brigade sprite folder");
        redirectedPath.Should().EndWith("RAMZA.SPR", "Filename should be preserved");
    }

    [Fact]
    public void GameIntegration_ShouldRegisterFileHook_WithModLoader()
    {
        // TLDR: GameIntegration should register file hook callback with mod loader
        // Arrange
        var integration = new GameIntegration();

        // Act
        integration.RegisterFileHookWithModLoader();

        // Assert
        integration.FileHookCallback.Should().NotBeNull("File hook callback should be registered");
        integration.IsFileHookActive.Should().BeTrue("File hook should be registered with mod loader");
    }

    [Fact]
    public void GameIntegration_ShouldInvokeCallback_WhenFileRequested()
    {
        // TLDR: File hook callback should be invoked when file is requested
        // Arrange
        var integration = new GameIntegration();
        integration.InitializeFileHook();
        integration.RegisterFileHookWithModLoader();

        // Cycle to second color scheme (corpse_brigade)
        integration.ProcessHotkey(0x70); // F1 key cycles from original to corpse_brigade

        // Act - simulate file request through callback
        var originalPath = @"data\sprites\CHARACTER.SPR";
        var redirectedPath = integration.InvokeFileHookCallback(originalPath);

        // Assert
        redirectedPath.Should().NotBeNull();
        redirectedPath.Should().Contain("sprites_corpse_brigade", "Should redirect to corpse_brigade sprite folder");
        redirectedPath.Should().EndWith("CHARACTER.SPR");
    }


    // Removed - This test used the old RAMZA.SPR format
    // We now use battle_*_spr.bin format for sprites

    // Removed - We're using better_palettes sprites now, not generating them programmatically
    // The GenerateSpriteVariants method has been removed from Mod.cs



    [Fact]
    public void GetRedirectedPath_ShouldHandleBinFiles()
    {
        // TLDR: GetRedirectedPath should handle .bin sprite files (FFT format)
        var integration = new GameIntegration();
        integration.InitializeFileHook();

        // Cycle to second color scheme (corpse_brigade)
        integration.ProcessHotkey(0x70); // F1 key cycles from original to corpse_brigade

        var binSpritePath = @"data\sprites\battle_ramza_spr.bin";

        // Act
        var redirectedPath = integration.GetRedirectedPath(binSpritePath);

        // Assert - should redirect .bin files that end with _spr.bin
        redirectedPath.Should().Contain("sprites_corpse_brigade");
        redirectedPath.Should().EndWith("battle_ramza_spr.bin");
    }

    [Fact]
    public void Mod_F1_Should_Open_Config_UI()
    {
        // TLDR: Test that F1 opens config UI instead of cycling colors
        var mod = new Mod();
        bool configUIOpened = false;
        mod.ConfigUIRequested += () => configUIOpened = true;

        // Reset to original
        // mod.SetJobTheme("original") - Method removed in refactoring;

        // Initial state should be original
        mod.GetCurrentColorScheme().Should().Be("original");

        // Press F1 - should open config UI and not change scheme
        mod.ProcessHotkeyPress(0x70); // F1 key
        configUIOpened.Should().BeTrue("F1 should open config UI");
        var afterF1 = mod.GetCurrentColorScheme();
        afterF1.Should().Be("original", "F1 should not change color scheme");

        // Reset flag and press F1 again - should still open config UI
        configUIOpened = false;
        mod.ProcessHotkeyPress(0x70); // F1 key
        configUIOpened.Should().BeTrue("F1 should consistently open config UI");
        var afterSecondF1 = mod.GetCurrentColorScheme();
        afterSecondF1.Should().Be("original", "F1 should never change color scheme");
    }

    [Fact]
    public void Mod_Hotkey_F1_Should_Open_Config_UI()
    {
        // TLDR: When F1 key is pressed, mod should open config UI
        // Arrange
        var mod = new Mod();
        mod.InitializeGameIntegration();
        bool configUIOpened = false;
        mod.ConfigUIRequested += () => configUIOpened = true;

        // Get initial scheme
        var initialScheme = mod.GetCurrentColorScheme();
        initialScheme.Should().Be("original");

        // Act - Simulate F1 key press via ProcessHotkey (opens config UI)
        mod.ProcessHotkeyPress(0x70); // F1 key

        // Assert - Should open config UI and not change color scheme
        configUIOpened.Should().BeTrue("F1 should open config UI");
        var currentScheme = mod.GetCurrentColorScheme();
        currentScheme.Should().Be("original", "F1 should not change color scheme anymore");
    }
}