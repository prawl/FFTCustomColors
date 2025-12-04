using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Reloaded.Mod.Interfaces;
using Reloaded.Hooks.Definitions;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using Reloaded.Memory.Sigscan;
using Reloaded.Memory.Sigscan.Structs;
using Reloaded.Memory.Sigscan.Definitions.Structs;

namespace FFTColorMod;

/// <summary>
/// Your mod logic goes here.
/// </summary>
public class Mod : IMod
{
    private GameIntegration? _gameIntegration;
    private Task? _hotkeyTask;
    private CancellationTokenSource? _cancellationTokenSource;
    private Process? _gameProcess;
    private IntPtr _processHandle;

    // Hooking infrastructure
    private SignatureScanner? _signatureScanner;
    private IReloadedHooks? _hooks;
    private IHook<LoadSpriteDelegate>? _loadSpriteHook;

    // Delegate for sprite loading function
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr LoadSpriteDelegate(IntPtr spriteData, int size);

    public Mod()
    {
        Console.WriteLine("[FFT Color Mod] Default constructor called!");

        // Initialize immediately in constructor since Start() doesn't get called
        InitializeInConstructor();
    }

    private void InitializeInConstructor()
    {
        try
        {
            Console.WriteLine("[FFT Color Mod] Initializing in constructor...");

            _gameProcess = Process.GetCurrentProcess();
            Console.WriteLine($"[FFT Color Mod] Game process ID: {_gameProcess.Id}");
            Console.WriteLine($"[FFT Color Mod] Game base: 0x{_gameProcess.MainModule?.BaseAddress.ToInt64():X}");

            _processHandle = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION,
                                        false, _gameProcess.Id);

            // Initialize game integration
            _gameIntegration = new GameIntegration();
            _gameIntegration.StartMonitoring();

            // Start hotkey monitoring in background
            _cancellationTokenSource = new CancellationTokenSource();
            _hotkeyTask = Task.Run(() => MonitorHotkeys(_cancellationTokenSource.Token));

            Console.WriteLine("[FFT Color Mod] Loaded successfully!");
            Console.WriteLine("[FFT Color Mod] Press F1 for original colors, F2 for red colors");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FFT Color Mod] Error during initialization: {ex.Message}");
        }
    }

    // Keep Start method even if not called
    public void Start(IModLoader modLoader)
    {
        Console.WriteLine("[FFT Color Mod] Start() called - attempting experimental hooks");

        try
        {
            // Initialize signature scanner
            _signatureScanner = new SignatureScanner();
            _signatureScanner.SetPaletteDetector(_gameIntegration?.PaletteDetector ?? new PaletteDetector());

            Console.WriteLine("[FFT Color Mod] SignatureScanner initialized");

            // Try to get services - these might not be available in all Reloaded versions
            // Using a simple approach that logs what's available
            Console.WriteLine("[FFT Color Mod] Note: Hook services may not be available - this is experimental");
            Console.WriteLine("[FFT Color Mod] Experimental patterns ready for manual testing:");
            Console.WriteLine("[FFT Color Mod]   - 48 8B C4 48 89 58 ?? (common function prologue)");
            Console.WriteLine("[FFT Color Mod]   - 48 89 5C 24 ?? 48 89 74 24 ?? (stack frame setup)");
            Console.WriteLine("[FFT Color Mod]   - 40 53 48 83 EC ?? (push rbx, sub rsp)");
            Console.WriteLine("[FFT Color Mod] If patterns are found during gameplay, they will be logged");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FFT Color Mod] Error in Start(): {ex.Message}");
            Console.WriteLine($"[FFT Color Mod] Stack trace: {ex.StackTrace}");
        }
    }

    // Hook function that intercepts sprite loading
    private IntPtr LoadSpriteHook(IntPtr spriteData, int size)
    {
        Console.WriteLine($"[FFT Color Mod] LoadSpriteHook called! spriteData=0x{spriteData.ToInt64():X}, size={size}");

        // Call original function first
        var result = _loadSpriteHook?.OriginalFunction(spriteData, size) ?? spriteData;

        // Apply palette modification if we have a valid color scheme
        if (_signatureScanner != null && _signatureScanner.ColorScheme != "original")
        {
            Console.WriteLine($"[FFT Color Mod] Applying {_signatureScanner.ColorScheme} color scheme to sprite data");

            // TODO: Use PaletteDetector to identify and modify the palette
            // For now, just log
            result = _signatureScanner.ProcessSpriteData(spriteData, size);
        }

        return result;
    }

    // Windows API for memory operations
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        [Out] byte[] lpBuffer,
        int dwSize,
        out IntPtr lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        byte[] lpBuffer,
        int nSize,
        out IntPtr lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(
        uint dwDesiredAccess,
        bool bInheritHandle,
        int dwProcessId);

    // Process access flags
    private const uint PROCESS_VM_READ = 0x0010;
    private const uint PROCESS_VM_WRITE = 0x0020;
    private const uint PROCESS_VM_OPERATION = 0x0008;

    // Windows API for hotkey detection
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_F1 = 0x70;
    private const int VK_F2 = 0x71;

    private void MonitorHotkeys(CancellationToken cancellationToken)
    {
        Console.WriteLine("[FFT Color Mod] Hotkey monitoring thread started");
        bool f1WasPressed = false;
        bool f2WasPressed = false;
        int loopCount = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Log every 100 loops (5 seconds) to show thread is alive
                if (loopCount % 100 == 0)
                {
                    Console.WriteLine($"[FFT Color Mod] Hotkey thread alive, checking keys... (loop {loopCount})");
                }
                loopCount++;

                // Check F1 key
                short f1State = GetAsyncKeyState(VK_F1);
                bool f1Pressed = (f1State & 0x8000) != 0;
                if (f1Pressed && !f1WasPressed)
                {
                    Console.WriteLine("[FFT Color Mod] F1 PRESSED - Switching to original colors");
                    ApplyColorScheme("original");
                    Console.WriteLine("[FFT Color Mod] Switched to original colors");
                }
                f1WasPressed = f1Pressed;

                // Check F2 key
                short f2State = GetAsyncKeyState(VK_F2);
                bool f2Pressed = (f2State & 0x8000) != 0;
                if (f2Pressed && !f2WasPressed)
                {
                    Console.WriteLine("[FFT Color Mod] F2 PRESSED - Switching to red colors");
                    ApplyColorScheme("red");
                    Console.WriteLine("[FFT Color Mod] Switched to red colors");
                }
                f2WasPressed = f2Pressed;

                Thread.Sleep(50); // Check every 50ms
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FFT Color Mod] Error in hotkey monitoring: {ex.Message}");
                Console.WriteLine($"[FFT Color Mod] Stack trace: {ex.StackTrace}");
            }
        }
        Console.WriteLine("[FFT Color Mod] Hotkey monitoring thread stopped");
    }

    private void ApplyColorScheme(string scheme)
    {
        Console.WriteLine($"[FFT Color Mod] ApplyColorScheme called with scheme: {scheme}");

        // Update the SignatureScanner's color scheme for hook-based modifications
        if (_signatureScanner != null)
        {
            _signatureScanner.SetColorScheme(scheme);
            Console.WriteLine($"[FFT Color Mod] SignatureScanner color scheme set to: {scheme}");
        }

        if (_gameIntegration == null || _processHandle == IntPtr.Zero || _gameProcess == null)
        {
            Console.WriteLine("[FFT Color Mod] ApplyColorScheme: Missing required objects");
            return;
        }

        try
        {
            // Update the hotkey manager
            _gameIntegration.HotkeyManager.ProcessHotkey(scheme == "original" ? VK_F1 : VK_F2);
            Console.WriteLine($"[FFT Color Mod] HotkeyManager updated with scheme: {scheme}");

            // EXPANDED MEMORY SEARCH: Find active rendering palettes across multiple regions
            const int searchSize = 1024 * 1024 * 10; // 10MB per region

            IntPtr baseAddress = _gameProcess.MainModule?.BaseAddress ?? IntPtr.Zero;
            Console.WriteLine($"[FFT Color Mod] Game base address: 0x{baseAddress.ToInt64():X}");

            // ULTRA-EXPANDED: Try even higher memory regions for GPU-accessible palettes
            long[] tryOffsets = {
                // Original ranges (cached palettes found here)
                0, 0x1000000, 0x2000000, 0x3000000, 0x4000000, 0x5000000,
                // Extended ranges for active rendering memory
                0x6000000, 0x7000000, 0x8000000, 0x9000000, 0xA000000, 0xB000000,
                0xC000000, 0xD000000, 0xE000000, 0xF000000, 0x10000000, 0x12000000,
                0x14000000, 0x16000000, 0x18000000, 0x1A000000, 0x1C000000, 0x1E000000,
                // Graphics memory regions (found 8 palettes here but no visual changes)
                0x20000000, 0x30000000, 0x40000000, 0x50000000, 0x60000000, 0x70000000,
                // ULTRA-HIGH: GPU-accessible rendering memory (where live palettes should be)
                0x80000000, 0x90000000, 0xA0000000, 0xB0000000, 0xC0000000, 0xD0000000,
                0xE0000000, 0xF0000000, 0x100000000, 0x120000000, 0x140000000, 0x160000000,
                0x180000000, 0x1A0000000, 0x1C0000000, 0x1E0000000, 0x200000000, 0x300000000
            };

            // Collect all readable memory regions
            var memoryRegions = new List<(byte[] data, long baseOffset)>();

            foreach (var offsetToTry in tryOffsets)
            {
                IntPtr searchAddress = IntPtr.Add(baseAddress, (int)Math.Min(offsetToTry, int.MaxValue));

                byte[] buffer = new byte[searchSize];
                IntPtr bytesRead;
                bool success = ReadProcessMemory(_processHandle, searchAddress, buffer, searchSize, out bytesRead);

                if (success && bytesRead.ToInt64() > 0)
                {
                    memoryRegions.Add((buffer, offsetToTry));
                    Console.WriteLine($"[FFT Color Mod] Read {bytesRead.ToInt64()} bytes from 0x{searchAddress.ToInt64():X}");
                }
            }

            Console.WriteLine($"[FFT Color Mod] Collected {memoryRegions.Count} memory regions for scanning");

            // MULTIPLE WRITE STRATEGY: Find ALL palettes across ALL regions
            var allFoundPalettes = _gameIntegration.MemoryScanner.ScanForAllPalettesInMemoryRegions(
                memoryRegions, _gameIntegration.PaletteDetector);

            if (allFoundPalettes.Count > 0)
            {
                Console.WriteLine($"[FFT Color Mod] Found {allFoundPalettes.Count} total palette(s) across all memory regions");

                foreach (var palette in allFoundPalettes)
                {
                    Console.WriteLine($"[FFT Color Mod] Palette at memory 0x{(baseAddress.ToInt64() + palette.memoryAddress):X} is Chapter {palette.chapter}");

                    if (palette.chapter > 0)
                    {
                        // Find the corresponding memory region and buffer
                        var region = memoryRegions.FirstOrDefault(r => r.baseOffset == (palette.memoryAddress - palette.bufferOffset));
                        if (region.data != null)
                        {
                            // Apply color scheme
                            if (scheme != "original")
                            {
                                // Log original colors
                                Console.WriteLine($"[FFT Color Mod] Original colors at buffer offset {palette.bufferOffset:X}:");
                                for (int i = 0; i < 9; i++) // Show first 3 colors (9 bytes)
                                {
                                    if (palette.bufferOffset + i < region.data.Length)
                                        Console.Write($"{region.data[palette.bufferOffset + i]:X2} ");
                                }
                                Console.WriteLine();

                                // Modify colors in buffer
                                _gameIntegration.MemoryScanner.ApplyColorScheme(
                                    region.data, palette.bufferOffset, scheme, _gameIntegration.PaletteDetector, palette.chapter);

                                // Log new colors
                                Console.WriteLine($"[FFT Color Mod] New colors at buffer offset {palette.bufferOffset:X}:");
                                for (int i = 0; i < 9; i++) // Show first 3 colors (9 bytes)
                                {
                                    if (palette.bufferOffset + i < region.data.Length)
                                        Console.Write($"{region.data[palette.bufferOffset + i]:X2} ");
                                }
                                Console.WriteLine();

                                // CRITICAL: Write to actual memory address
                                byte[] modifiedData = new byte[256];
                                Buffer.BlockCopy(region.data, palette.bufferOffset, modifiedData, 0, 256);

                                IntPtr writeAddress = IntPtr.Add(baseAddress, (int)(palette.memoryAddress));
                                IntPtr bytesWritten;
                                bool writeSuccess = WriteProcessMemory(_processHandle, writeAddress,
                                                 modifiedData, modifiedData.Length, out bytesWritten);
                                Console.WriteLine($"[FFT Color Mod] WriteProcessMemory to 0x{writeAddress.ToInt64():X}: success={writeSuccess}, bytesWritten={bytesWritten.ToInt64()}");
                            }
                            else
                            {
                                Console.WriteLine($"[FFT Color Mod] Skipping modification (original colors requested)");
                            }
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("[FFT Color Mod] No palettes found in any memory region searched");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FFT Color Mod] Error applying color scheme: {ex.Message}");
            Console.WriteLine($"[FFT Color Mod] Stack trace: {ex.StackTrace}");
        }
    }

    public void Suspend()
    {
        _gameIntegration?.StopMonitoring();
    }

    public void Resume()
    {
        _gameIntegration?.StartMonitoring();
    }

    public void Unload()
    {
        _cancellationTokenSource?.Cancel();
        _hotkeyTask?.Wait(1000);
        _gameIntegration?.StopMonitoring();
        _cancellationTokenSource?.Dispose();
    }

    public bool CanUnload() => true;
    public bool CanSuspend() => true;

    public Action Disposing { get; } = () => { };
}