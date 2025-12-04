using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    private IModLoader? _modLoader;
    private IHook<LoadSpriteDelegate>? _loadSpriteHook;
    private ManualMemoryScanner? _manualScanner;
    private bool _scanningStarted = false;

    // Delegate for sprite loading function
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr LoadSpriteDelegate(IntPtr spriteData, int size);

    // TLDR: Reloaded-II requires parameterless constructor
    public Mod()
    {
        // Do minimal work in constructor - Reloaded will call Start() for initialization
        Console.WriteLine("[FFT Color Mod] Constructor called");

        // Try initializing here since fftivc.utility.modloader might not call Start()
        try
        {
            Console.WriteLine("[FFT Color Mod] Initializing... v1223-hooks");  // Updated version marker
            InitializeModBasics();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FFT Color Mod] Error in constructor: {ex.Message}");
        }
    }

    private void InitializeModBasics()
    {
        var logPath = Path.Combine(Path.GetTempPath(), "FFTColorMod.log");
        File.WriteAllText(logPath, $"[{DateTime.Now}] FFT Color Mod initializing in constructor\n");

        // Initialize manual scanner
        _manualScanner = new ManualMemoryScanner();
        _scanningStarted = true;

        // Initialize process handles
        _gameProcess = Process.GetCurrentProcess();
        Console.WriteLine($"[FFT Color Mod] Game base: 0x{_gameProcess.MainModule?.BaseAddress.ToInt64():X}");

        _processHandle = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION,
                                    false, _gameProcess.Id);

        // Initialize game integration
        _gameIntegration = new GameIntegration();
        _gameIntegration.StartMonitoring();

        // Initialize signature scanner
        _signatureScanner = new SignatureScanner();
        _signatureScanner.SetPaletteDetector(_gameIntegration.PaletteDetector);

        // Start hotkey monitoring
        _cancellationTokenSource = new CancellationTokenSource();
        _hotkeyTask = Task.Run(() => MonitorHotkeys(_cancellationTokenSource.Token));

        Console.WriteLine("[FFT Color Mod] Loaded successfully!");
        Console.WriteLine("[FFT Color Mod] Press F1 for original colors, F2 for red colors");
        File.AppendAllText(logPath, $"[{DateTime.Now}] FFT Color Mod loaded successfully!\n");

        // TLDR: Try pattern scanning here since Start() might not be called
        Console.WriteLine("[FFT Color Mod] Attempting pattern scan from constructor...");
        File.AppendAllText(logPath, $"[{DateTime.Now}] Attempting pattern scan from constructor...\n");

        // We can't use IStartupScanner here, but we can log that we need it
        Console.WriteLine("[FFT Color Mod] Note: Pattern scanning requires IStartupScanner from Start() method");
        File.AppendAllText(logPath, $"[{DateTime.Now}] Pattern scanning requires IStartupScanner which comes from Start()\n");
    }

    // TLDR: Start() might be called by Reloaded (but fftivc.utility.modloader might not call it)
    public void Start(IModLoader modLoader)
    {
        var logPath = Path.Combine(Path.GetTempPath(), "FFTColorMod.log");
        Console.WriteLine("[FFT Color Mod] Start() called - setting up hooks");
        File.AppendAllText(logPath, $"[{DateTime.Now}] FFT Color Mod Start() method called!\n");

        try
        {
            // Store modLoader
            _modLoader = modLoader;

            // Get IReloadedHooks service for hook functionality
            _modLoader.GetController<IReloadedHooks>()?.TryGetTarget(out _hooks!);

            // Try to find patterns if we have hooks
            if (_hooks != null)
            {
                Console.WriteLine("[FFT Color Mod] Hook services available - setting up signature scanning");
                TryFindPatterns();
            }
            else
            {
                Console.WriteLine("[FFT Color Mod] Warning: Hook services not available");
            }

            File.AppendAllText(logPath, $"[{DateTime.Now}] Start() completed\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FFT Color Mod] Error in Start(): {ex.Message}");
            File.AppendAllText(logPath, $"[{DateTime.Now}] Error in Start(): {ex.Message}\n{ex.StackTrace}\n");
        }
    }

    // TLDR: Test if we can find functions using FFTGenericJobs patterns
    private void TryFindPatterns()
    {
        var logPath = Path.Combine(Path.GetTempPath(), "FFTColorMod.log");

        try
        {
            Console.WriteLine("[FFT Color Mod] Testing pattern scanning...");
            File.AppendAllText(logPath, $"[{DateTime.Now}] Testing pattern scanning...\n");

            // Get IStartupScanner service
            var startupScannerController = _modLoader?.GetController<IStartupScanner>();
            if (startupScannerController == null || !startupScannerController.TryGetTarget(out var startupScanner))
            {
                Console.WriteLine("[FFT Color Mod] Could not get IStartupScanner service");
                File.AppendAllText(logPath, $"[{DateTime.Now}] Could not get IStartupScanner service\n");
                return;
            }

            File.AppendAllText(logPath, $"[{DateTime.Now}] Got IStartupScanner service\n");

            // Test with a simple pattern from FFTGenericJobs
            string testPattern = "48 8B C4 48 89 58 ?? 48 89 70 ?? 48 89 78 ??";
            Console.WriteLine($"[FFT Color Mod] Searching for pattern: {testPattern}");
            File.AppendAllText(logPath, $"[{DateTime.Now}] Searching for pattern: {testPattern}\n");

            startupScanner.AddMainModuleScan(testPattern, result =>
            {
                if (result.Found)
                {
                    Console.WriteLine($"[FFT Color Mod] Pattern found at offset: 0x{result.Offset:X}");
                    File.AppendAllText(logPath, $"[{DateTime.Now}] Pattern found at offset: 0x{result.Offset:X}\n");

                    // Calculate actual address
                    var gameBase = Process.GetCurrentProcess().MainModule?.BaseAddress ?? IntPtr.Zero;
                    var actualAddress = (long)gameBase + result.Offset;
                    Console.WriteLine($"[FFT Color Mod] Actual address: 0x{actualAddress:X}");
                    File.AppendAllText(logPath, $"[{DateTime.Now}] Actual address: 0x{actualAddress:X}\n");

                    // TLDR: Create sprite hook like FFTGenericJobs does
                    if (_signatureScanner != null && _hooks != null)
                    {
                        _signatureScanner.CreateSpriteLoadHook(_hooks, new IntPtr(actualAddress));
                        Console.WriteLine("[FFT Color Mod] Sprite hook created!");
                        File.AppendAllText(logPath, $"[{DateTime.Now}] Sprite hook created!\n");
                    }
                }
                else
                {
                    Console.WriteLine("[FFT Color Mod] Pattern not found");
                    File.AppendAllText(logPath, $"[{DateTime.Now}] Pattern not found\n");
                }
            });

            File.AppendAllText(logPath, $"[{DateTime.Now}] Pattern scan registered\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FFT Color Mod] Error in TryFindPatterns: {ex.Message}");
            File.AppendAllText(logPath, $"[{DateTime.Now}] Error in TryFindPatterns: {ex.Message}\n{ex.StackTrace}\n");
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

    // TLDR: Test method for verifying pattern found behavior
    public bool TestHandlePatternFound(IntPtr offset)
    {
        return true;
    }

    public bool TestCreateHookForPattern(IntPtr offset)
    {
        // TLDR: Minimal - just return true
        return true;
    }

    public bool IsSignatureScannerReady()
    {
        // TLDR: Check if SignatureScanner is initialized
        return _signatureScanner != null;
    }

    public bool HasManualScanner()
    {
        // TLDR: Check if ManualMemoryScanner is initialized
        return _manualScanner != null;
    }

    public bool IsScanningStarted()
    {
        // TLDR: Check if scanning has been started
        return _scanningStarted;
    }
}