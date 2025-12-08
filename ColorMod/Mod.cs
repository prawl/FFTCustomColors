using System;
using System.Diagnostics;
using System.IO;
using FFTColorMod.Utilities;
using Reloaded.Mod.Interfaces;

namespace FFTColorMod;

/// <summary>
/// Your mod logic goes here.
/// </summary>
public class Mod : IMod
{
    private GameIntegration? _gameIntegration;
    private HotkeyHandler? _hotkeyHandler;
    private SpriteFileManager? _spriteFileManager;
    private Process? _gameProcess;
    private string _currentColorScheme = "original";
    private ColorSchemeCycler _colorCycler;

    // Constructor that accepts ModContext (new pattern from FFTGenericJobs)
    public Mod(ModContext context)
    {
        // Do minimal work in constructor - Reloaded will call Start() for initialization
        Console.WriteLine("[FFT Color Mod] Constructor called with ModContext");

        _colorCycler = new ColorSchemeCycler();
        _colorCycler.SetCurrentScheme("original");

        // Try initializing here since fftivc.utility.modloader might not call Start()
        try
        {
            Console.WriteLine("[FFT Color Mod] Initializing... v1223-file-swap-only");  // File swap only version
            InitializeModBasics();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FFT Color Mod] Error in constructor: {ex.Message}");
        }
    }

    // TLDR: Keep parameterless constructor for backward compatibility
    public Mod() : this(new ModContext())
    {
    }

    private void InitializeModBasics()
    {
        var logPath = Path.Combine(Path.GetTempPath(), "FFTColorMod.log");
        File.WriteAllText(logPath, $"[{DateTime.Now}] FFT Color Mod initializing in constructor\n");

        // Initialize process handles
        _gameProcess = Process.GetCurrentProcess();
        Console.WriteLine($"[FFT Color Mod] Game base: 0x{_gameProcess.MainModule?.BaseAddress.ToInt64():X}");

        // Initialize sprite file manager
        string modPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;
        _spriteFileManager = new SpriteFileManager(modPath);

        // Start with original color scheme (file swapping persists across restarts)
        _currentColorScheme = "original";
        Console.WriteLine($"[FFT Color Mod] Starting with color scheme: {_currentColorScheme}");

        // Initialize game integration
        _gameIntegration = new GameIntegration();

        // Initialize and start hotkey handler
        _hotkeyHandler = new HotkeyHandler(ProcessHotkeyPress);
        _hotkeyHandler.StartMonitoring();

        Console.WriteLine("[FFT Color Mod] Loaded successfully!");
        Console.WriteLine("[FFT Color Mod] Press F1 to cycle through color schemes");
        File.AppendAllText(logPath, $"[{DateTime.Now}] FFT Color Mod loaded successfully!\n");
    }

    // TLDR: Start() might be called by Reloaded (but fftivc.utility.modloader might not call it)
    public void Start(IModLoader modLoader)
    {
        var logPath = Path.Combine(Path.GetTempPath(), $"FFTColorMod_{Guid.NewGuid()}.log");
        Console.WriteLine("[FFT Color Mod] Start() called - setting up hooks");

        try
        {
            File.AppendAllText(logPath, $"[{DateTime.Now}] FFT Color Mod Start() method called!\n");
        }
        catch
        {
            // Ignore file write errors in tests
        }

        try
        {

            // File swapping only - no memory hooks
            Console.WriteLine("[FFT Color Mod] File swapping mode enabled - Press F1 to cycle through color schemes!");

            File.AppendAllText(logPath, $"[{DateTime.Now}] Start() completed\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FFT Color Mod] Error in Start(): {ex.Message}");
            File.AppendAllText(logPath, $"[{DateTime.Now}] Error in Start(): {ex.Message}\n{ex.StackTrace}\n");
        }
    }

    public void Suspend()
    {
    }

    public void Resume()
    {
    }

    public void Unload()
    {
        Console.WriteLine("[FFT Color Mod] Unloading...");
        _hotkeyHandler?.StopMonitoring();
        _gameProcess?.Dispose();

        Console.WriteLine("[FFT Color Mod] Unloaded");
    }

    // TLDR: ModId property for other mods to identify this mod
    public string ModId => "FFTColorMod";

    // Cannot unload while mod is active
    public bool CanUnload() => false;

    // Cannot suspend while mod is active
    public bool CanSuspend() => false;

    // TLDR: Indicates support for per-character color customization
    public bool SupportsPerCharacterColors() => true;

    public Action Disposing { get; } = () => { };

    public void InitializeGameIntegration()
    {
        // TLDR: Initialize GameIntegration for file hooks
        if (_gameIntegration == null)
        {
            _gameIntegration = new GameIntegration();
        }

        // Initialize SpriteFileManager if not already done
        if (_spriteFileManager == null)
        {
            string modPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;
            _spriteFileManager = new SpriteFileManager(modPath);
        }

        // Always initialize file hooks when this method is called
        _gameIntegration.InitializeFileHook();
        _gameIntegration.RegisterFileHookWithModLoader();
    }

    public bool HasGameIntegration()
    {
        // TLDR: Check if GameIntegration is initialized
        return _gameIntegration != null;
    }

    public bool IsFileRedirectionActive()
    {
        // TLDR: Check if file redirection is active
        return _gameIntegration?.IsFileHookActive ?? false;
    }

    public void SetColorScheme(string scheme)
    {
        _currentColorScheme = scheme;
        Console.WriteLine($"[FFT Color Mod] Color scheme set to: {scheme}");

        // Actually switch the sprite files to apply the color
        _spriteFileManager?.SwitchColorScheme(scheme);

        // Update cycler's current scheme
        _colorCycler?.SetCurrentScheme(scheme);

    }


    public void ProcessHotkeyPress(int vkCode)
    {
        const int VK_F1 = 0x70;

        if (vkCode == VK_F1)
        {
            // Cycle to next color
            string nextColor = _colorCycler.GetNextScheme();
            Console.WriteLine($"[FFT Color Mod] Cycling to {nextColor}");
            SetColorScheme(nextColor);
        }
    }

    public string GetCurrentColorScheme()
    {
        // TLDR: Get the currently active color scheme
        return _currentColorScheme;
    }

    public string InterceptFilePath(string originalPath)
    {
        return _spriteFileManager?.InterceptFilePath(originalPath, _currentColorScheme) ?? originalPath;
    }
} 