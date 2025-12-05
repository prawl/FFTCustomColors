using System;
using System.IO;
using System.Threading;
using Reloaded.Hooks.Definitions;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;

namespace FFTColorMod;

// Minimal implementation to make the test pass
public class Startup : IMod
{
    private Mod _mod = null!;
    private SpriteMemoryHooker? _memoryHooker;
    private HotkeyManager _hotkeyManager = null!;
    private PaletteDetector _paletteDetector = null!;
    private Thread? _hotkeyThread;
    private bool _running = true;
    public ColorPreferencesManager ColorPreferencesManager { get; private set; } = null!;

    public Startup()
    {
        // TLDR: Initialize ColorPreferencesManager with config file in AppData
        var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FFTColorMod");
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, "preferences.json");
        ColorPreferencesManager = new ColorPreferencesManager(configPath);

        // Initialize core components
        _hotkeyManager = new HotkeyManager();
        _paletteDetector = new PaletteDetector();
    }

    // This is the method that Reloaded-II actually calls!
    public void StartEx(IModLoaderV1 loaderApi, IModConfigV1 modConfig)
    {
        Console.WriteLine("[FFTColorMod] Starting with memory hooking for real-time color changes!");

        // Try to get hooking services
        var hooksController = loaderApi?.GetController<IReloadedHooks>();
        var scannerController = loaderApi?.GetController<IStartupScanner>();

        if (hooksController != null && scannerController != null &&
            hooksController.TryGetTarget(out var hooks) &&
            scannerController.TryGetTarget(out var scanner))
        {
            Console.WriteLine("[FFTColorMod] Hook services available - initializing memory hooks");

            // Initialize memory hooking for real-time color changes
            _memoryHooker = new SpriteMemoryHooker(hooks, scanner, _paletteDetector, _hotkeyManager);
            _memoryHooker.InitializeHooks();

            // Start hotkey monitoring
            StartHotkeyMonitoring();

            Console.WriteLine("[FFTColorMod] Memory hooks initialized! Press F1-F5 to change colors in real-time!");
        }
        else
        {
            Console.WriteLine("[FFTColorMod] Hook services not available - falling back to standard mode");
        }

        // Create ModContext with services (will expand later)
        var context = new ModContext();

        // Create our mod instance with context
        _mod = new Mod(context);
    }

    private void StartHotkeyMonitoring()
    {
        _hotkeyThread = new Thread(() =>
        {
            Console.WriteLine("[FFTColorMod] Hotkey monitoring started - F1-F5 to change colors");

            while (_running)
            {
                // Check for F1-F5 keys
                for (int keyCode = 0x70; keyCode <= 0x74; keyCode++)
                {
                    if ((GetAsyncKeyState(keyCode) & 0x8000) != 0)
                    {
                        var oldScheme = _hotkeyManager.CurrentScheme;
                        _hotkeyManager.ProcessHotkey(keyCode);

                        if (_hotkeyManager.CurrentScheme != oldScheme)
                        {
                            var schemeName = keyCode switch
                            {
                                0x70 => "ORIGINAL",
                                0x71 => "RED",
                                0x72 => "BLUE",
                                0x73 => "GREEN",
                                0x74 => "PURPLE",
                                _ => "UNKNOWN"
                            };

                            Console.WriteLine($"[FFTColorMod] Switched to {schemeName} colors - sprites will update in real-time!");
                        }

                        // Debounce
                        Thread.Sleep(500);
                    }
                }

                Thread.Sleep(50);
            }
        })
        {
            IsBackground = true,
            Name = "FFTColorMod Hotkey Monitor"
        };

        _hotkeyThread.Start();
    }

    // Import Windows API for key state checking
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    // Required by IMod interface - minimal implementation
    public void Suspend() { }
    public void Resume() { }
    public void Unload() { }
    public bool CanUnload() => false;
    public bool CanSuspend() => false;
    public Action Disposing { get; } = () => { };
}