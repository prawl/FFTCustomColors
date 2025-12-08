using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Reloaded.Mod.Interfaces;

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
    private ColorPreferencesManager? _preferencesManager;
    private string _currentColorScheme = "original";
    private PaletteDetector? _paletteDetector;
    private HotkeyManager? _hotkeyManager;
    private ColorSchemeCycler _colorCycler;

    // Constructor that accepts ModContext (new pattern from FFTGenericJobs)
    public Mod(ModContext context)
    {
        // Do minimal work in constructor - Reloaded will call Start() for initialization
        Console.WriteLine("[FFT Color Mod] Constructor called with ModContext");

        // TLDR: Always initialize PaletteDetector even if other initialization fails
        _paletteDetector = new PaletteDetector();
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

        // Initialize preferences manager and load saved preferences
        var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FFTColorMod");
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, "preferences.json");
        _preferencesManager = new ColorPreferencesManager(configPath);

        // Load and apply saved color preference
        var savedScheme = _preferencesManager.LoadPreferences();
        var scheme = savedScheme switch
        {
            ColorScheme.WhiteSilver => "white_silver",
            ColorScheme.OceanBlue => "ocean_blue",
            ColorScheme.DeepPurple => "deep_purple",
            _ => "original"
        };
        _currentColorScheme = scheme;
        Console.WriteLine($"[FFT Color Mod] Loaded saved preference: {scheme}");

        // Apply the saved color scheme
        SwitchPacFile(scheme);

        // Initialize game integration
        _gameIntegration = new GameIntegration();
        // Monitoring removed - file swapping only

        // Signature scanner removed - not needed

        // Start hotkey monitoring
        _cancellationTokenSource = new CancellationTokenSource();
        _hotkeyTask = Task.Run(() => MonitorHotkeys(_cancellationTokenSource.Token));

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
            // Initialize HotkeyManager if not already done
            if (_hotkeyManager == null)
            {
                _hotkeyManager = new HotkeyManager();
                Console.WriteLine("[FFT Color Mod] Created HotkeyManager");
            }

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




    // Windows API for hotkey detection
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_F1 = 0x70;
    private const int VK_F2 = 0x71;

    private void MonitorHotkeys(CancellationToken cancellationToken)
    {
        Console.WriteLine("[FFT Color Mod] Hotkey monitoring thread started");
        bool f1WasPressed = false;
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

                // Check F1 key - Cycle colors
                short f1State = GetAsyncKeyState(VK_F1);
                bool f1Pressed = (f1State & 0x8000) != 0;
                if (f1Pressed && !f1WasPressed)
                {
                    Console.WriteLine("[FFT Color Mod] F1 PRESSED - Cycling to next color scheme");
                    ProcessHotkeyPress(VK_F1);
                }
                f1WasPressed = f1Pressed;

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

    private void SwitchPacFile(string color)
    {
        try
        {
            // Get the mod's directory structure for sprites
            var modDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var unitDir = Path.Combine(modDir, "FFTIVC", "data", "enhanced", "fftpack", "unit");

            Console.WriteLine($"[FFT Color Mod] Switching to {color} color scheme");
            Console.WriteLine($"[FFT Color Mod] Unit directory: {unitDir}");

            if (!Directory.Exists(unitDir))
            {
                Console.WriteLine($"[FFT Color Mod] ERROR: Unit directory not found: {unitDir}");
                return;
            }

            // Get the source directory for the selected color
            string sourceDir;
            if (color == "original")
            {
                sourceDir = Path.Combine(unitDir, "sprites_original");
            }
            else
            {
                sourceDir = Path.Combine(unitDir, $"sprites_{color}");
            }

            Console.WriteLine($"[FFT Color Mod] Source directory: {sourceDir}");

            if (!Directory.Exists(sourceDir))
            {
                Console.WriteLine($"[FFT Color Mod] WARNING: Color variant directory not found: {sourceDir}");
                Console.WriteLine($"[FFT Color Mod] Color variants need to be generated first!");
                return;
            }

            // Get all sprite files from the color variant directory
            var spriteFiles = Directory.GetFiles(sourceDir, "*.bin");

            if (spriteFiles.Length == 0)
            {
                Console.WriteLine($"[FFT Color Mod] WARNING: No sprite files found in {sourceDir}");
                Console.WriteLine($"[FFT Color Mod] Run sprite color generation first to create variants!");
                return;
            }

            Console.WriteLine($"[FFT Color Mod] Found {spriteFiles.Length} sprite files to copy");

            // Copy all sprite files from the color directory to the base unit directory
            int copiedCount = 0;
            foreach (var sourceFile in spriteFiles)
            {
                var fileName = Path.GetFileName(sourceFile);
                var destFile = Path.Combine(unitDir, fileName);

                try
                {
                    File.Copy(sourceFile, destFile, overwrite: true);
                    copiedCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FFT Color Mod] Failed to copy {fileName}: {ex.Message}");
                }
            }

            Console.WriteLine($"[FFT Color Mod] Successfully copied {copiedCount} sprite files for {color} color scheme");

            // Note: Reloaded-II should automatically apply these overrides
            // The game will load the modified sprites from FFTIVC/data/enhanced/fftpack/unit/
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FFT Color Mod] Error switching sprites: {ex.Message}");
            Console.WriteLine($"[FFT Color Mod] Stack trace: {ex.StackTrace}");
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
        _cancellationTokenSource?.Cancel();
        _hotkeyTask?.Wait(1000);
        _cancellationTokenSource?.Dispose();
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
        // TLDR: Public method to set color scheme and save preference
        _currentColorScheme = scheme;

        // Save preference if manager is available (regardless of game integration)
        if (_preferencesManager != null)
        {
            var colorScheme = scheme switch
            {
                "original" => ColorScheme.Original,
                "default" => ColorScheme.WhiteSilver,  // Map to existing enum for now
                "corpse_brigade" => ColorScheme.OceanBlue,  // Map to existing enum for now
                "lucavi" => ColorScheme.DeepPurple,  // Map to existing enum for now
                _ => ColorScheme.Original
            };
            _preferencesManager.SavePreferences(colorScheme);
        }

        // Game integration removed - file swapping only
    }

    public void SetPreferencesPath(string path)
    {
        // TLDR: Set path for preferences file
        _preferencesManager = new ColorPreferencesManager(path);
    }

    public void ProcessHotkeyPress(int vkCode)
    {
        // TLDR: Process a hotkey press and update color scheme
        string scheme = null;

        if (vkCode == VK_F1) // F1 cycles through schemes
        {
            scheme = _colorCycler.GetNextScheme();
            _colorCycler.SetCurrentScheme(scheme);
        }

        if (scheme != null)
        {
            SetColorScheme(scheme);
            SwitchPacFile(scheme);
        }
    }

    public string GetCurrentColorScheme()
    {
        // TLDR: Get the currently active color scheme
        return _currentColorScheme;
    }

    public string InterceptFilePath(string originalPath)
    {
        // TLDR: Intercept file path and redirect based on active color scheme
        if (!originalPath.Contains("sprites"))
            return originalPath;

        // Use the mod's current color scheme
        if (_currentColorScheme == "original" || string.IsNullOrEmpty(_currentColorScheme))
            return originalPath;

        // Replace sprites folder with color variant folder
        return originalPath.Replace(@"sprites\", $@"sprites_{_currentColorScheme}\");
    }

}