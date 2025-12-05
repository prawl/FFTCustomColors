using System;
using System.Runtime.InteropServices;
using Reloaded.Hooks.Definitions;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;

namespace FFTColorMod;

public class SpriteMemoryHooker
{
    private readonly IReloadedHooks _hooks;
    private readonly IStartupScanner _scanner;
    private readonly PaletteDetector _paletteDetector;
    private readonly HotkeyManager _hotkeyManager;

    private IHook<DrawSpriteDelegate>? _drawSpriteHook;

    // FFT uses a simpler sprite drawing function
    // We target specifically sprite drawing, not general rendering
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DrawSpriteDelegate(IntPtr spriteData);

    public SpriteMemoryHooker(IReloadedHooks hooks, IStartupScanner scanner, PaletteDetector paletteDetector, HotkeyManager hotkeyManager)
    {
        _hooks = hooks;
        _scanner = scanner;
        _paletteDetector = paletteDetector;
        _hotkeyManager = hotkeyManager;
    }

    public void InitializeHooks()
    {
        Console.WriteLine("[FFTColorMod] Initializing sprite memory hooks (safer version)...");

        var gameBase = System.Diagnostics.Process.GetCurrentProcess().MainModule?.BaseAddress ?? IntPtr.Zero;
        Console.WriteLine($"[FFTColorMod] Game base address: 0x{gameBase.ToInt64():X}");

        // More specific pattern for FFT sprite drawing
        // This pattern is specific to character sprite rendering, not map rendering
        // We look for the pattern that loads sprite palette data specifically
        string spriteDrawPattern = "8B 44 24 ?? 8B 4C 24 ?? 50 51 E8 ?? ?? ?? ?? 83 C4 08";

        // Alternative pattern if first doesn't work
        string altSpritePattern = "55 8B EC 83 EC ?? 53 56 57 8B 7D ?? 8B 77";

        bool hookFound = false;

        // Try first pattern
        _scanner.AddMainModuleScan(spriteDrawPattern, result =>
        {
            if (!hookFound && result.Found)
            {
                var address = gameBase + result.Offset;
                Console.WriteLine($"[FFTColorMod] Found sprite draw function at 0x{address.ToInt64():X}");

                try
                {
                    CreateSafeHook(address);
                    hookFound = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FFTColorMod] Failed to hook at primary address: {ex.Message}");
                }
            }
        });

        // Try alternative pattern if first fails
        if (!hookFound)
        {
            _scanner.AddMainModuleScan(altSpritePattern, result =>
            {
                if (!hookFound && result.Found)
                {
                    var address = gameBase + result.Offset;
                    Console.WriteLine($"[FFTColorMod] Found alternate sprite function at 0x{address.ToInt64():X}");

                    try
                    {
                        CreateSafeHook(address);
                        hookFound = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[FFTColorMod] Failed to hook at alternate address: {ex.Message}");
                    }
                }
            });
        }

        // If no hooks found, try palette interception instead
        if (!hookFound)
        {
            Console.WriteLine("[FFTColorMod] No sprite patterns found, trying palette memory monitoring...");
            StartPaletteMonitoring();
        }

        Console.WriteLine("[FFTColorMod] Hotkeys are active:");
        Console.WriteLine("  F1: Original colors");
        Console.WriteLine("  F2: Red colors");
        Console.WriteLine("  F3: Blue colors");
        Console.WriteLine("  F4: Green colors");
        Console.WriteLine("  F5: Purple colors");
    }

    private void CreateSafeHook(IntPtr address)
    {
        _drawSpriteHook = _hooks.CreateHook<DrawSpriteDelegate>(
            DrawSpriteHook,
            address.ToInt64()
        );

        _drawSpriteHook.Activate();
        _drawSpriteHook.Enable();
        Console.WriteLine("[FFTColorMod] Sprite draw hook activated!");
    }

    private void DrawSpriteHook(IntPtr spriteData)
    {
        try
        {
            // Safety check - only process if we have valid data
            if (spriteData == IntPtr.Zero)
            {
                _drawSpriteHook!.OriginalFunction(spriteData);
                return;
            }

            // Only modify if not on original colors
            if (_hotkeyManager.CurrentScheme != "original")
            {
                // Try to find palette data near the sprite data
                // FFT typically stores palette right before or after sprite data
                IntPtr palettePtr = IntPtr.Zero;

                // Check for palette signature (16 colors = 48 bytes for one palette line)
                // Look for palette data pattern in nearby memory
                for (int offset = -512; offset <= 512; offset += 16)
                {
                    try
                    {
                        IntPtr checkPtr = IntPtr.Add(spriteData, offset);
                        byte[] testData = new byte[48];
                        Marshal.Copy(checkPtr, testData, 0, 48);

                        // Check if this looks like palette data
                        if (IsPaletteData(testData))
                        {
                            palettePtr = checkPtr;
                            break;
                        }
                    }
                    catch
                    {
                        // Skip invalid memory regions
                    }
                }

                if (palettePtr != IntPtr.Zero)
                {
                    ModifyPaletteInPlace(palettePtr);
                }
            }
        }
        catch (Exception ex)
        {
            // Don't crash the game, just skip modification
            Console.WriteLine($"[FFTColorMod] Hook error (non-fatal): {ex.Message}");
        }

        // Always call original function
        _drawSpriteHook!.OriginalFunction(spriteData);
    }

    private bool IsPaletteData(byte[] data)
    {
        // Check if this looks like palette data
        // Palettes have certain characteristics:
        // - RGB values typically don't exceed certain ranges
        // - First color is often transparent (0,0,0) or background
        // - Colors follow patterns

        if (data.Length < 48) return false;

        // Check if it matches known palette patterns
        int validColors = 0;
        for (int i = 0; i < Math.Min(48, data.Length); i += 3)
        {
            // Check for reasonable RGB values
            if (data[i] <= 31 && data[i + 1] <= 31 && data[i + 2] <= 31)
            {
                validColors++;
            }
        }

        // If most values look like 5-bit color values, it's probably a palette
        return validColors >= 12;
    }

    private void ModifyPaletteInPlace(IntPtr palettePtr)
    {
        try
        {
            // Read full palette (288 bytes for full sprite palette)
            byte[] paletteData = new byte[288];
            Marshal.Copy(palettePtr, paletteData, 0, 288);

            // Detect which sprite this is
            var detectedChapter = _paletteDetector.DetectChapterOutfit(paletteData, 0);

            if (detectedChapter > 0)
            {
                // Apply color modification
                _paletteDetector.ReplacePaletteColors(paletteData, 0, _hotkeyManager.CurrentScheme, detectedChapter);

                // Write back to memory
                Marshal.Copy(paletteData, 0, palettePtr, 288);
            }
        }
        catch
        {
            // Silently fail to avoid spamming or crashes
        }
    }

    private void StartPaletteMonitoring()
    {
        // Alternative approach: monitor known palette memory regions
        // This is safer but might not catch all sprites
        Console.WriteLine("[FFTColorMod] Using palette memory monitoring approach");

        // We'll use a timer to periodically check and modify palette memory
        // This is less efficient but safer than hooking rendering functions
        var timer = new System.Timers.Timer(100); // Check every 100ms
        timer.Elapsed += (sender, e) =>
        {
            try
            {
                CheckAndModifyPalettes();
            }
            catch
            {
                // Ignore errors to prevent crashes
            }
        };
        timer.Start();
    }

    private void CheckAndModifyPalettes()
    {
        // This would scan known memory regions for palette data
        // Implementation would require research into FFT's memory layout
        // For now, this is a placeholder for a safer approach
    }

    public void Disable()
    {
        _drawSpriteHook?.Disable();
        Console.WriteLine("[FFTColorMod] Memory hooks disabled");
    }
}