using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using FFTColorMod.Configuration;
using FFTColorMod.Utilities;
using Reloaded.Mod.Interfaces;

namespace FFTColorMod;

/// <summary>
/// Your mod logic goes here.
/// </summary>
public class Mod : IMod, IConfigurable
{
    private GameIntegration? _gameIntegration;
    private HotkeyHandler? _hotkeyHandler;
    private SpriteFileManager? _spriteFileManager;
    private ConfigBasedSpriteManager? _configBasedSpriteManager;
    private ConfigurationManager? _configurationManager;
    private Process? _gameProcess;
    private string _currentColorScheme = "original";
    private ColorSchemeCycler _colorCycler;
    private IInputSimulator? _inputSimulator;
    private bool _configUIRequested = false;
    private string _modPath;

    // Constructor that accepts ModContext and optional IInputSimulator (for testing)
    public Mod(ModContext context, IInputSimulator? inputSimulator = null)
    {
        // Do minimal work in constructor - Reloaded will call Start() for initialization
        Console.WriteLine("[FFT Color Mod] Constructor called with ModContext");
        _inputSimulator = inputSimulator;

        // Try to auto-detect sprite variants from mod directory
        _modPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;
        string spritesPath = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

        _colorCycler = new ColorSchemeCycler(spritesPath);
        var schemes = _colorCycler.GetAvailableSchemes();

        if (schemes.Count > 0)
        {
            Console.WriteLine($"[FFT Color Mod] Auto-detected {schemes.Count} color schemes");
            _colorCycler.SetCurrentScheme("original");
        }
        else
        {
            Console.WriteLine("[FFT Color Mod] No color schemes found in: " + spritesPath);
            // Still set a default even if no schemes found
            _colorCycler.SetCurrentScheme("original");
        }

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

        // Initialize sprite file manager and configuration
        string modPath = Environment.GetEnvironmentVariable("FFT_MOD_PATH") ??
                        Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ??
                        Environment.CurrentDirectory;
        _spriteFileManager = new SpriteFileManager(modPath);

        // Initialize configuration-based sprite management
        string configPath = Environment.GetEnvironmentVariable("FFT_CONFIG_PATH") ??
                           Path.Combine(modPath, "config.json");
        Console.WriteLine($"[FFT Color Mod] Loading config from: {configPath}");
        _configurationManager = new ConfigurationManager(configPath);
        _configBasedSpriteManager = new ConfigBasedSpriteManager(modPath, _configurationManager);

        // Load saved configuration
        var loadedConfig = _configurationManager.LoadConfig();
        Console.WriteLine($"[FFT Color Mod] Loaded config - Knight_Male: {loadedConfig.Knight_Male}");
        _configBasedSpriteManager.ApplyConfiguration();

        // Initialize input simulator if not provided (for testing)
        if (_inputSimulator == null)
        {
            _inputSimulator = new InputSimulator();
        }

        // Start with original color scheme (file swapping persists across restarts)
        _currentColorScheme = "original";
        Console.WriteLine($"[FFT Color Mod] Starting with color scheme: {_currentColorScheme}");

        // Initialize game integration
        _gameIntegration = new GameIntegration();

        // Initialize and start hotkey handler
        _hotkeyHandler = new HotkeyHandler(ProcessHotkeyPress);
        _hotkeyHandler.StartMonitoring();

        Console.WriteLine("[FFT Color Mod] Loaded successfully!");
        Console.WriteLine("[FFT Color Mod] Press F1 (previous) or F2 (next) to cycle through color schemes");
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
        const int VK_F2 = 0x71;
        const int VK_F3 = 0x72;

        if (vkCode == VK_F1)
        {
            // Cycle to previous color
            string previousColor = _colorCycler.GetPreviousScheme();
            Console.WriteLine($"[FFT Color Mod] Cycling backward to {previousColor}");
            SetColorScheme(previousColor);

            // Simulate menu refresh to update sprites immediately
            Console.WriteLine($"[FFT Color Mod] InputSimulator is {(_inputSimulator != null ? "available" : "NULL")}");
            if (_inputSimulator != null)
            {
                Console.WriteLine("[FFT Color Mod] Calling SimulateMenuRefresh...");
                bool result = _inputSimulator.SimulateMenuRefresh();
                Console.WriteLine($"[FFT Color Mod] SimulateMenuRefresh returned: {result}");
            }
            else
            {
                Console.WriteLine("[FFT Color Mod] WARNING: InputSimulator is null!");
            }
        }
        else if (vkCode == VK_F2)
        {
            // Cycle to next color
            string nextColor = _colorCycler.GetNextScheme();
            Console.WriteLine($"[FFT Color Mod] Cycling forward to {nextColor}");
            SetColorScheme(nextColor);

            // Simulate menu refresh to update sprites immediately
            Console.WriteLine($"[FFT Color Mod] InputSimulator is {(_inputSimulator != null ? "available" : "NULL")}");
            if (_inputSimulator != null)
            {
                Console.WriteLine("[FFT Color Mod] Calling SimulateMenuRefresh...");
                bool result = _inputSimulator.SimulateMenuRefresh();
                Console.WriteLine($"[FFT Color Mod] SimulateMenuRefresh returned: {result}");
            }
            else
            {
                Console.WriteLine("[FFT Color Mod] WARNING: InputSimulator is null!");
            }
        }
        else if (vkCode == VK_F3)
        {
            // Open configuration UI
            _configUIRequested = true;
            Console.WriteLine("[FFT Color Mod] Configuration UI requested (F3)");
        }
    }

    public string GetCurrentColorScheme()
    {
        // TLDR: Get the currently active color scheme
        return _currentColorScheme;
    }

    public string InterceptFilePath(string originalPath)
    {
        // Check if this is a job sprite that should be handled by config-based system
        var fileName = Path.GetFileName(originalPath);

        // If we have a config-based manager and this is a recognized job sprite
        if (_configBasedSpriteManager != null && IsJobSprite(fileName))
        {
            return _configBasedSpriteManager.InterceptFilePath(originalPath);
        }

        // Fall back to old system for non-job sprites or when config manager not available
        return _spriteFileManager?.InterceptFilePath(originalPath, _currentColorScheme) ?? originalPath;
    }

    private bool IsJobSprite(string fileName)
    {
        // Check if this matches any of the job sprite patterns
        var jobPatterns = new[] {
            "battle_knight_", "battle_yumi_", "battle_item_", "battle_monk_",
            "battle_siro_", "battle_kuro_", "battle_thief_", "battle_ninja_",
            "battle_mina_", "battle_toki_", "battle_syou_", "battle_samu_",
            "battle_ryu_", "battle_fusui_", "battle_onmyo_", "battle_waju_",
            "battle_odori_", "battle_gin_", "battle_mono_", "battle_san_"
        };

        return jobPatterns.Any(pattern => fileName.StartsWith(pattern, StringComparison.OrdinalIgnoreCase));
    }

    // Configuration-based methods
    public bool HasConfigurationManager()
    {
        return _configurationManager != null;
    }

    public void SetJobColor(string jobProperty, string colorScheme)
    {
        _configBasedSpriteManager?.SetColorForJob(jobProperty, colorScheme);
    }

    public string GetJobColor(string jobProperty)
    {
        return _configBasedSpriteManager?.GetActiveColorForJob(jobProperty) ?? "original";
    }

    public Dictionary<string, string> GetAllJobColors()
    {
        var result = new Dictionary<string, string>();
        if (_configurationManager != null)
        {
            var config = _configurationManager.LoadConfig();
            var properties = typeof(Config).GetProperties()
                .Where(p => p.PropertyType == typeof(string) &&
                           (p.Name.EndsWith("Male") || p.Name.EndsWith("Female")));

            foreach (var property in properties)
            {
                var value = property.GetValue(config) as string;
                result[property.Name] = string.IsNullOrEmpty(value) ? "original" : value;
            }
        }
        return result;
    }

    public bool IsConfigUIRequested()
    {
        return _configUIRequested;
    }

    public void ResetAllColors()
    {
        _configBasedSpriteManager?.ResetAllToOriginal();
    }

    // IConfigurable implementation
    public string ConfigName => "FFT Color Mod Configuration";

    public Action Save => () =>
    {
        // Save current configuration
        _configurationManager?.SaveConfig(_configurationManager.LoadConfig());
    };

    public void InitializeConfiguration(string configPath)
    {
        // Initialize the configuration manager with custom path for testing
        _configurationManager = new ConfigurationManager(configPath);
        _configBasedSpriteManager = new ConfigBasedSpriteManager(Path.GetDirectoryName(configPath), _configurationManager);
    }

    public void ConfigurationUpdated(Config configuration)
    {
        // Initialize configuration manager if not already initialized
        if (_configurationManager == null)
        {
            var defaultPath = Path.Combine(_modPath, "config.json");
            InitializeConfiguration(defaultPath);
        }

        // Update the configuration manager with the new config
        _configurationManager?.SaveConfig(configuration);

        // Apply the new configuration
        _configBasedSpriteManager?.ApplyConfiguration();

        Console.WriteLine("[FFT Color Mod] Configuration updated from Reloaded-II UI");
    }
} 