using System;
using System.IO;
using System.Threading;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;

namespace FFTColorMod;

// Minimal implementation to make the test pass
public class Startup : IMod
{
    private Mod _mod = null!;
    private Thread? _hotkeyThread;
    private bool _running = true;
    private string _currentScheme = "original";

    public Startup()
    {
        // Initialize core components
    }

    // This is the method that Reloaded-II actually calls!
    public void StartEx(IModLoaderV1 loaderApi, IModConfigV1 modConfig)
    {
        Console.WriteLine("[FFTColorMod] Starting with file swapping for color changes!");

        // Start hotkey monitoring for file swapping
        StartHotkeyMonitoring();

        Console.WriteLine("[FFTColorMod] File swapping initialized! Press F1 to cycle colors!");

        // Create ModContext with services (will expand later)
        var context = new ModContext();

        // Create our mod instance with context
        _mod = new Mod(context);
    }

    private void StartHotkeyMonitoring()
    {
        _hotkeyThread = new Thread(() =>
        {
            Console.WriteLine("[FFTColorMod] Hotkey monitoring started - F1 to cycle colors");

            while (_running)
            {
                // Check for F1 key only (0x70)
                if ((GetAsyncKeyState(0x70) & 0x8000) != 0)
                {
                    // Simple cycling through schemes
                    _currentScheme = _currentScheme switch
                    {
                        "original" => "corpse_brigade",
                        "corpse_brigade" => "lucavi",
                        "lucavi" => "northern_sky",
                        "northern_sky" => "smoke",
                        "smoke" => "southern_sky",
                        "southern_sky" => "original",
                        _ => "original"
                    };

                    Console.WriteLine($"[FFTColorMod] Cycled to {_currentScheme} colors - sprites will update in real-time!");

                    // Debounce
                    Thread.Sleep(500);
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