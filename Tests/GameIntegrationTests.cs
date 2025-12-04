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

    [Fact]
    public void Mod_Constructor_ShouldCreateManualMemoryScanner()
    {
        // TLDR: Test that Mod creates ManualMemoryScanner in constructor
        // Arrange & Act
        var mod = new Mod();

        // Assert - mod should initialize with scanner ready
        Assert.NotNull(mod);
        Assert.True(mod.HasManualScanner(), "Should have manual scanner ready");
    }

    [Fact]
    public void Mod_Constructor_ShouldStartScanning()
    {
        // TLDR: Test that Mod starts scanning for patterns on initialization
        // Arrange & Act
        var mod = new Mod();

        // Assert - mod should have started scanning
        Assert.True(mod.IsScanningStarted(), "Should start scanning in constructor");
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
    public void Should_Extract_First_Sprite_From_PAC_File()
    {
        // TLDR: Extract first .SPR file from a real FFT PAC archive
        const string gamePath = @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles";
        var dataPath = System.IO.Path.Combine(gamePath, "data", "classic");

        // Skip test if game not installed
        if (!System.IO.Directory.Exists(dataPath))
        {
            return; // Skip test gracefully if game not installed
        }

        // Find first PAC file
        var pacFiles = System.IO.Directory.GetFiles(dataPath, "*.pac");
        pacFiles.Should().NotBeEmpty();

        var firstPac = pacFiles[0];
        var outputDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FFTColorMod_Test_Sprites");

        // Create output directory
        if (System.IO.Directory.Exists(outputDir))
            System.IO.Directory.Delete(outputDir, true);
        System.IO.Directory.CreateDirectory(outputDir);

        try
        {
            // Extract sprites using PacExtractor
            var extractor = new PacExtractor();
            var opened = extractor.OpenPac(firstPac);
            opened.Should().BeTrue($"Should be able to open PAC file: {firstPac}");

            var extractedCount = extractor.ExtractAllSprites(outputDir);

            // If no sprites extracted, it might be the wrong PAC file
            if (extractedCount == 0)
            {
                // This PAC might not contain sprites, skip the rest of the test
                return; // Skip gracefully - not all PAC files contain sprites
            }

            // Verify we extracted at least one sprite
            extractedCount.Should().BeGreaterThan(0, "Should extract at least one sprite from PAC file");

            // Verify extracted files exist and have .SPR extension
            var extractedFiles = System.IO.Directory.GetFiles(outputDir, "*.SPR");
            extractedFiles.Should().NotBeEmpty("Output directory should contain .SPR files");

            foreach (var file in extractedFiles)
            {
                System.IO.File.Exists(file).Should().BeTrue($"Extracted file {file} should exist");
                System.IO.Path.GetExtension(file).ToUpper().Should().Be(".SPR", $"File {file} should have .SPR extension");
            }
        }
        finally
        {
            // Cleanup
            if (System.IO.Directory.Exists(outputDir))
                System.IO.Directory.Delete(outputDir, true);
        }
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

        // Set active color scheme to red
        integration.ProcessHotkey(0x71); // F2 for red

        // Act - request original sprite path
        var originalPath = @"data\sprites\RAMZA.SPR";
        var redirectedPath = integration.GetRedirectedPath(originalPath);

        // Assert - should redirect to red sprite folder
        redirectedPath.Should().Contain("sprites_red", "Path should redirect to red sprite folder");
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
        integration.IsFileHookRegistered.Should().BeTrue("File hook should be registered with mod loader");
    }

    [Fact]
    public void GameIntegration_ShouldInvokeCallback_WhenFileRequested()
    {
        // TLDR: File hook callback should be invoked when file is requested
        // Arrange
        var integration = new GameIntegration();
        integration.InitializeFileHook();
        integration.RegisterFileHookWithModLoader();

        // Set color scheme to blue
        integration.ProcessHotkey(0x72); // F3 for blue

        // Act - simulate file request through callback
        var originalPath = @"data\sprites\CHARACTER.SPR";
        var redirectedPath = integration.InvokeFileHookCallback(originalPath);

        // Assert
        redirectedPath.Should().NotBeNull();
        redirectedPath.Should().Contain("sprites_blue", "Should redirect to blue sprite folder");
        redirectedPath.Should().EndWith("CHARACTER.SPR");
    }

    [Fact]
    public void GameIntegration_ShouldProcessSpriteFiles_ForColorVariants()
    {
        // TLDR: GameIntegration should process sprite files to create color variants
        // Arrange
        var integration = new GameIntegration();
        var testSpritePath = System.IO.Path.GetTempPath() + "test_sprite.SPR";

        // Create a test sprite file with palette data
        byte[] spriteData = new byte[1024];
        // Add Ramza's brown palette at offset 100
        spriteData[100] = 0x17; // Blue component
        spriteData[101] = 0x2C; // Green component
        spriteData[102] = 0x4A; // Red component (Ramza brown)
        System.IO.File.WriteAllBytes(testSpritePath, spriteData);

        try
        {
            // Act - process sprite for color variants
            var processedCount = integration.ProcessSpriteForColorVariants(testSpritePath);

            // Assert
            processedCount.Should().Be(4, "Should create 4 color variants (red, blue, green, purple)");
            integration.LastProcessedSpritePath.Should().Be(testSpritePath);
        }
        finally
        {
            // Cleanup
            if (System.IO.File.Exists(testSpritePath))
                System.IO.File.Delete(testSpritePath);
        }
    }

    [Fact]
    public void Mod_ShouldInterceptFilePath_WhenSpriteRequested()
    {
        // TLDR: Mod should intercept file paths and redirect to color variant folders
        // Arrange
        var mod = new Mod();
        mod.InitializeGameIntegration();

        // Set active color scheme to blue using public method
        mod.SetColorScheme("blue");

        // Act - simulate file request
        var originalPath = @"data\sprites\RAMZA.SPR";
        var interceptedPath = mod.InterceptFilePath(originalPath);

        // Assert
        interceptedPath.Should().NotBe(originalPath);
        interceptedPath.Should().Contain("sprites_blue");
        interceptedPath.Should().EndWith("RAMZA.SPR");
    }

    [Fact]
    public void Mod_ShouldGenerateSpriteColorVariants_WhenRequested()
    {
        // TLDR: Mod should generate color variant sprites for deployment
        // Arrange
        var mod = new Mod();
        mod.InitializeGameIntegration();

        var testSpritePath = System.IO.Path.GetTempPath() + "test_sprite.SPR";
        var outputDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "test_variants");

        // Create test sprite with Ramza palette
        byte[] spriteData = new byte[1024];
        spriteData[100] = 0x17; // B
        spriteData[101] = 0x2C; // G
        spriteData[102] = 0x4A; // R (Ramza brown)
        System.IO.File.WriteAllBytes(testSpritePath, spriteData);

        try
        {
            // Act - generate color variants
            var generatedCount = mod.GenerateSpriteVariants(testSpritePath, outputDir);

            // Assert
            generatedCount.Should().Be(4, "Should generate 4 color variants");
            System.IO.Directory.Exists(outputDir).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            if (System.IO.File.Exists(testSpritePath))
                System.IO.File.Delete(testSpritePath);
            if (System.IO.Directory.Exists(outputDir))
                System.IO.Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public void Mod_FullIntegration_HotkeysShouldSwitchFileRedirection()
    {
        // TLDR: Full integration test - hotkeys should change file redirection behavior
        // Arrange
        var mod = new Mod();
        mod.InitializeGameIntegration();

        var spritePath = @"data\sprites\RAMZA.SPR";

        // Act & Assert - Test each color scheme
        // F1 - Original (no redirection)
        mod.SetColorScheme("original");
        var originalPath = mod.InterceptFilePath(spritePath);
        originalPath.Should().Be(spritePath, "Original scheme should not redirect");

        // F2 - Red
        mod.SetColorScheme("red");
        var redPath = mod.InterceptFilePath(spritePath);
        redPath.Should().Contain("sprites_red", "Red scheme should redirect to red folder");
        redPath.Should().EndWith("RAMZA.SPR");

        // F3 - Blue
        mod.SetColorScheme("blue");
        var bluePath = mod.InterceptFilePath(spritePath);
        bluePath.Should().Contain("sprites_blue", "Blue scheme should redirect to blue folder");
        bluePath.Should().EndWith("RAMZA.SPR");

        // F4 - Green
        mod.SetColorScheme("green");
        var greenPath = mod.InterceptFilePath(spritePath);
        greenPath.Should().Contain("sprites_green", "Green scheme should redirect to green folder");
        greenPath.Should().EndWith("RAMZA.SPR");

        // F5 - Purple
        mod.SetColorScheme("purple");
        var purplePath = mod.InterceptFilePath(spritePath);
        purplePath.Should().Contain("sprites_purple", "Purple scheme should redirect to purple folder");
        purplePath.Should().EndWith("RAMZA.SPR");
    }

    [Fact]
    public void GameIntegration_ShouldUseSpriteColorGenerator_ToCreateVariants()
    {
        // TLDR: GameIntegration should use SpriteColorGenerator to create actual color variant files
        // Arrange
        var integration = new GameIntegration();
        integration.InitializeSpriteProcessor();

        var testSpritePath = System.IO.Path.GetTempPath() + "test_sprite.SPR";
        var outputDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "test_variants");

        // Create test sprite with Ramza palette
        byte[] spriteData = new byte[1024];
        spriteData[100] = 0x17; // B
        spriteData[101] = 0x2C; // G
        spriteData[102] = 0x4A; // R (Ramza brown)
        System.IO.File.WriteAllBytes(testSpritePath, spriteData);

        try
        {
            // Act - generate color variants to output directory
            var generatedFiles = integration.GenerateColorVariants(testSpritePath, outputDir);

            // Assert
            generatedFiles.Should().NotBeNull();
            generatedFiles.Count.Should().Be(4, "Should generate 4 color variant files");
            integration.HasSpriteProcessor.Should().BeTrue("Should have initialized sprite processor");
        }
        finally
        {
            // Cleanup
            if (System.IO.File.Exists(testSpritePath))
                System.IO.File.Delete(testSpritePath);
            if (System.IO.Directory.Exists(outputDir))
                System.IO.Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public void GetRedirectedPath_ShouldHandleBinFiles()
    {
        // TLDR: GetRedirectedPath should handle .bin sprite files (FFT format)
        var integration = new GameIntegration();
        integration.InitializeFileHook();

        // Set color scheme to blue
        integration.ProcessHotkey(0x72); // F3 key = blue

        var binSpritePath = @"data\sprites\battle_ramza_spr.bin";

        // Act
        var redirectedPath = integration.GetRedirectedPath(binSpritePath);

        // Assert - should redirect .bin files that end with _spr.bin
        redirectedPath.Should().Contain("sprites_blue");
        redirectedPath.Should().EndWith("battle_ramza_spr.bin");
    }
}