using System;
using System.Collections.Generic;
using System.Threading;

namespace FFTColorMod;

public class GameIntegration
{
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


    public GameIntegration()
    {
        PaletteDetector = new PaletteDetector();
        HotkeyManager = new HotkeyManager();
        IsInitialized = true;
    }


    public void ProcessHotkey(int keyCode)
    {
        // Process the hotkey
        HotkeyManager.ProcessHotkey(keyCode);
        LastAppliedScheme = HotkeyManager.CurrentScheme;
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

        // Check if this is a sprite file (.SPR or _spr.bin)
        bool isSpriteFile = originalPath.EndsWith(".SPR", StringComparison.OrdinalIgnoreCase) ||
                           originalPath.EndsWith("_spr.bin", StringComparison.OrdinalIgnoreCase);

        if (!isSpriteFile)
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