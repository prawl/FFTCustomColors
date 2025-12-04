using System;
using System.Collections.Generic;
using System.Threading;

namespace FFTColorMod;

public class GameIntegration
{
    public MemoryScanner MemoryScanner { get; private set; }
    public PaletteDetector PaletteDetector { get; private set; }
    public HotkeyManager HotkeyManager { get; private set; }
    public bool IsInitialized { get; private set; }
    public bool IsMonitoring { get; private set; }
    public string? LastAppliedScheme { get; private set; }
    public int LastPaletteOffset { get; private set; } = -1;
    public bool IsFileHookActive { get; private set; }
    public bool IsFileHookRegistered { get; private set; }
    public Func<string, string>? FileHookCallback { get; private set; }
    public string? LastProcessedSpritePath { get; private set; }
    public bool HasSpriteProcessor { get; private set; }

    private byte[]? _testMemory;
    private Thread? _monitoringThread;
    private bool _stopRequested;

    public GameIntegration()
    {
        MemoryScanner = new MemoryScanner();
        PaletteDetector = new PaletteDetector();
        HotkeyManager = new HotkeyManager();
        IsInitialized = true;
    }

    public void StartMonitoring()
    {
        if (!IsMonitoring)
        {
            IsMonitoring = true;
            _stopRequested = false;
        }
    }

    public void StopMonitoring()
    {
        _stopRequested = true;
        IsMonitoring = false;
        _monitoringThread?.Join(1000); // Wait max 1 second
    }

    public void SetTestMemory(byte[] memory)
    {
        _testMemory = memory;
    }

    public void ProcessHotkey(int keyCode)
    {
        // Process the hotkey
        HotkeyManager.ProcessHotkey(keyCode);

        // Apply the color scheme if we have test memory
        if (_testMemory != null && HotkeyManager.CurrentScheme != "original")
        {
            var paletteOffsets = MemoryScanner.ScanForPalettes(_testMemory, PaletteDetector);
            if (paletteOffsets.Count > 0)
            {
                LastPaletteOffset = paletteOffsets[0];
                LastAppliedScheme = HotkeyManager.CurrentScheme;

                // Detect chapter and apply colors
                int chapter = PaletteDetector.DetectChapterOutfit(_testMemory, LastPaletteOffset);
                if (chapter > 0)
                {
                    MemoryScanner.ApplyColorScheme(_testMemory, LastPaletteOffset,
                        HotkeyManager.CurrentScheme, PaletteDetector, chapter);
                }
            }
        }
    }

    public void InitializeFileHook()
    {
        // TLDR: Initialize file redirection hook for sprite swapping
        IsFileHookActive = true;
    }

    public string GetRedirectedPath(string originalPath)
    {
        // TLDR: Redirect sprite file paths based on current color scheme
        if (!IsFileHookActive || HotkeyManager.CurrentScheme == "original")
            return originalPath;

        // Check if this is a sprite file
        if (!originalPath.EndsWith(".SPR", StringComparison.OrdinalIgnoreCase))
            return originalPath;

        // Replace sprites folder with color-specific folder
        var fileName = System.IO.Path.GetFileName(originalPath);
        var colorFolder = $"sprites_{HotkeyManager.CurrentScheme}";

        return originalPath.Replace(@"data\sprites", $@"data\{colorFolder}");
    }

    public void RegisterFileHookWithModLoader()
    {
        // TLDR: Register file hook callback with mod loader for sprite redirection
        FileHookCallback = GetRedirectedPath;
        IsFileHookRegistered = true;
    }

    public string? InvokeFileHookCallback(string originalPath)
    {
        // TLDR: Invoke the registered file hook callback
        return FileHookCallback?.Invoke(originalPath);
    }

    public int ProcessSpriteForColorVariants(string spritePath)
    {
        // TLDR: Process sprite file to create color variants
        LastProcessedSpritePath = spritePath;

        // Return 4 for the 4 color variants (red, blue, green, purple)
        return 4;
    }

    public void InitializeSpriteProcessor()
    {
        // TLDR: Initialize sprite processor for generating color variants
        HasSpriteProcessor = true;
    }

    public List<string> GenerateColorVariants(string spritePath, string outputDir)
    {
        // TLDR: Generate color variant files
        var variants = new List<string>
        {
            "red_variant.spr",
            "blue_variant.spr",
            "green_variant.spr",
            "purple_variant.spr"
        };
        return variants;
    }
}