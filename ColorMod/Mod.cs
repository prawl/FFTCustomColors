using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using ColorMod.Registry;
using FFTColorMod.Configuration;
using FFTColorMod.Services;
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
    private DynamicSpriteLoader? _dynamicSpriteLoader;
    private Process? _gameProcess;
    private string _currentColorScheme = "original";
    private ColorSchemeCycler _colorCycler;
    private IInputSimulator? _inputSimulator;
    private string _modPath;
    private string _sourcePath; // Path to git repo for theme sources
    public event Action? ConfigUIRequested;

    // Constructor that accepts ModContext and optional IInputSimulator (for testing)
    public Mod(ModContext context, IInputSimulator? inputSimulator = null)
    {
        // Do minimal work in constructor - Reloaded will call Start() for initialization
        ModLogger.Log("Constructor called with ModContext");
        _inputSimulator = inputSimulator;

        // Try to auto-detect sprite variants from mod directory
        _modPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;

        // Set the source path to the git repo location (hardcoded for now, could be made configurable)
        _sourcePath = @"C:\Users\ptyRa\Dev\FFT_Color_Mod\ColorMod";

        // Use source path for detecting themes (from git repo)
        string spritesPath = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

        _colorCycler = new ColorSchemeCycler(spritesPath);
        var schemes = _colorCycler.GetAvailableSchemes();

        // Initialize the registry system
        InitializeRegistry();

        if (schemes.Count > 0)
        {
            ModLogger.Log($"Auto-detected {schemes.Count} color schemes");
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
            ModLogger.Log("Initializing... v1223-file-swap-only");  // File swap only version
            InitializeModBasics();
        }
        catch (Exception ex)
        {
            ModLogger.LogError($"in constructor: {ex.Message}");
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

        // Set source path if not already set (for backward compatibility)
        if (string.IsNullOrEmpty(_sourcePath))
        {
            _sourcePath = @"C:\Users\ptyRa\Dev\FFT_Color_Mod\ColorMod";
        }

        // Use ModInitializer to handle complex initialization
        _modInitializer = new ModInitializer(_modPath, _sourcePath);
        var initResult = _modInitializer.Initialize();

        if (!initResult.Success)
        {
            ModLogger.Log($"Initialization failed: {initResult.ErrorMessage}");
            return;
        }

        // Assign initialized components
        _gameProcess = initResult.GameProcess;
        _spriteFileManager = initResult.SpriteFileManager;
        _configurationManager = initResult.ConfigurationManager;
        _configBasedSpriteManager = initResult.ConfigBasedSpriteManager;
        _dynamicSpriteLoader = initResult.DynamicSpriteLoader;
        _gameIntegration = initResult.GameIntegration;

        // Initialize theme manager
        _themeManager = new ThemeManager(_sourcePath, _modPath);
        _themeManager.ApplyInitialThemes();

        // Initialize input simulator if not provided (for testing)
        if (_inputSimulator == null)
        {
            _inputSimulator = new InputSimulator();
        }

        // Initialize hotkey service
        _hotkeyService = new HotkeyService(
            SetColorScheme,
            () => _colorCycler.GetNextScheme(),
            () => _colorCycler.GetPreviousScheme(),
            () => _themeManager.CycleOrlandeauTheme(),
            () => _themeManager.CycleAgriasTheme(),
            () => _themeManager.CycleCloudTheme(),
            OpenConfigurationUI,
            _inputSimulator);

        _hotkeyService.ConfigUIRequested += () => ConfigUIRequested?.Invoke();

        // Start with original color scheme
        _currentColorScheme = "original";
        ModLogger.Log($"Starting with color scheme: {_currentColorScheme}");

        // Initialize and start hotkey handler
        _hotkeyHandler = new HotkeyHandler(vkCode => _hotkeyService.ProcessHotkeyPress(vkCode));
        _hotkeyHandler.StartMonitoring();

        ModLogger.Log("Loaded successfully!");
        ModLogger.Log("Press F1 (previous) or F2 (next) to cycle through color schemes");
        File.AppendAllText(logPath, $"[{DateTime.Now}] FFT Color Mod loaded successfully!\n");
    }

    // TLDR: Start() might be called by Reloaded (but fftivc.utility.modloader might not call it)
    public void Start(IModLoader modLoader)
    {
        var logPath = Path.Combine(Path.GetTempPath(), $"FFTColorMod_{Guid.NewGuid()}.log");
        ModLogger.Log("Start() called - setting up hooks");

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
            ModLogger.Log("File swapping mode enabled - Press F1 to cycle through color schemes!");

            File.AppendAllText(logPath, $"[{DateTime.Now}] Start() completed\n");
        }
        catch (Exception ex)
        {
            ModLogger.LogError($"in Start(): {ex.Message}");
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
        ModLogger.Log("Unloading...");
        _hotkeyHandler?.StopMonitoring();
        _gameProcess?.Dispose();

        ModLogger.Log("Unloaded");
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
            // Source path is the git repo location for reading theme files
            string sourcePath = @"C:\Users\ptyRa\Dev\FFT_Color_Mod\ColorMod";
            _spriteFileManager = new SpriteFileManager(modPath, sourcePath);
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
        ModLogger.Log($"Color scheme set to: {scheme}");

        // Actually switch the sprite files to apply the color
        _spriteFileManager?.SwitchColorScheme(scheme);

        // Update cycler ONLY if it's out of sync (for direct calls like tests/initialization)
        // The cycler updates itself during GetNext/GetPrevious, but not for direct SetColorScheme calls
        if (_colorCycler?.GetCurrentScheme() != scheme)
        {
            _colorCycler?.SetCurrentScheme(scheme);
        }
    }


    private ThemeManager? _themeManager;
    private HotkeyService? _hotkeyService;
    private ModInitializer? _modInitializer;



    public string GetCurrentColorScheme()
    {
        // TLDR: Get the currently active color scheme
        return _currentColorScheme;
    }

    public string InterceptFilePath(string originalPath)
    {
        // Check if this is a job sprite that should be handled by config-based system
        var fileName = Path.GetFileName(originalPath);

        // F1/F2 global scheme takes priority over per-character config
        // When global scheme is not "original", ALL sprites use it
        if (_currentColorScheme != "original" && _spriteFileManager != null)
        {
            return _spriteFileManager.InterceptFilePath(originalPath, _currentColorScheme);
        }

        // Only use config-based system when global scheme is "original"
        if (_configBasedSpriteManager != null && IsJobSprite(fileName))
        {
            ModLogger.Log($"Using config-based system for: {fileName}");
            var result = _configBasedSpriteManager.InterceptFilePath(originalPath);
            ModLogger.Log($"Config result: {originalPath} -> {result}");
            return result;
        }

        // Fall back to sprite file manager for non-job sprites
        return _spriteFileManager?.InterceptFilePath(originalPath, _currentColorScheme) ?? originalPath;
    }

    private bool IsJobSprite(string fileName)
    {
        // Check if this matches any of the job sprite patterns
        var jobPatterns = new[] {
            "battle_mina_",   // Squire - MUST BE FIRST for proper interception
            "battle_knight_", "battle_yumi_", "battle_item_", "battle_monk_",
            "battle_siro_", "battle_kuro_", "battle_thief_", "battle_ninja_",
            "battle_toki_", "battle_syou_", "battle_samu_",
            "battle_ryu_", "battle_fusui_", "battle_onmyo_", "battle_waju_",
            "battle_odori_", "battle_gin_", "battle_mono_", "battle_san_",
            // Story characters - MUST use actual sprite names from game
            "battle_musu_",   // Mustadio
            "battle_aguri_",  // Agrias (has two sprites)
            "battle_kanba_",  // Agrias second sprite
            "battle_oru_",    // Orlandeau (NOT oran!)
            "battle_dily",    // Delita (has dily, dily2, dily3)
            "battle_hime_",   // Ovelia
            "battle_aruma_",  // Alma
            "battle_rafa_",   // Rafa
            "battle_mara_",   // Malak
            "battle_cloud_",  // Cloud
            "battle_reze_",   // Reis (has reze and reze_d)
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
        ModLogger.Log($"SetJobColor called: {jobProperty} = {colorScheme}");
        ModLogger.Log($"_configBasedSpriteManager is null? {_configBasedSpriteManager == null}");

        if (_configBasedSpriteManager != null)
        {
            _configBasedSpriteManager.SetColorForJob(jobProperty, colorScheme);
        }
        else
        {
            ModLogger.LogWarning("_configBasedSpriteManager is null, cannot set job color");
        }
    }

    public string GetJobColor(string jobProperty)
    {
        return _configBasedSpriteManager?.GetActiveColorForJob(jobProperty) ?? "Original";
    }

    public Dictionary<string, string> GetAllJobColors()
    {
        var result = new Dictionary<string, string>();

        // Always load or create a config
        Config config = null;
        if (_configurationManager != null)
        {
            config = _configurationManager.LoadConfig();
        }
        else
        {
            // Create a default config if no manager
            config = new Config();
        }

        // Get all job properties
        var properties = typeof(Config).GetProperties()
            .Where(p => p.PropertyType == typeof(Configuration.ColorScheme) &&
                       (p.Name.EndsWith("_Male") || p.Name.EndsWith("_Female")));

        foreach (var property in properties)
        {
            var value = property.GetValue(config);
            if (value is Configuration.ColorScheme colorScheme)
            {
                result[property.Name] = colorScheme.GetDescription(); // Returns Description attribute
            }
            else
            {
                result[property.Name] = "Original";
            }
        }
        return result;
    }

    public bool IsConfigUIRequested()
    {
        return false; // This method appears to be unused but kept for API compatibility
    }

    // Public method for backward compatibility with tests
    public void ProcessHotkeyPress(int vkCode)
    {
        // If hotkey service isn't initialized yet (e.g., in tests), initialize it minimally
        if (_hotkeyService == null)
        {
            _hotkeyService = new HotkeyService(
                SetColorScheme,
                () => _colorCycler?.GetNextScheme() ?? "original",
                () => _colorCycler?.GetPreviousScheme() ?? "original",
                () => _themeManager?.CycleOrlandeauTheme(),
                () => _themeManager?.CycleAgriasTheme(),
                () => _themeManager?.CycleCloudTheme(),
                OpenConfigurationUI,
                _inputSimulator);

            _hotkeyService.ConfigUIRequested += () => ConfigUIRequested?.Invoke();
        }

        _hotkeyService.ProcessHotkeyPress(vkCode);
    }

    protected virtual void OpenConfigurationUI()
    {
        try
        {
            ModLogger.Log("Opening configuration UI...");

            // Load current configuration
            if (_configurationManager != null)
            {
                var config = _configurationManager.LoadConfig();

                // Use the same User directory path that the mod is using
                var reloadedRoot = Directory.GetParent(Directory.GetParent(_modPath).FullName)?.FullName ?? _modPath;
                var configPath = Path.Combine(reloadedRoot, "User", "Mods", "ptyra.fft.colormod", "Config.json");

                var configForm = new Configuration.ConfigurationForm(config, configPath);

                var result = configForm.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    ModLogger.Log("Saving configuration...");
                    _configurationManager.SaveConfig(config);

                    // Update the config-based sprite manager with new configuration
                    if (_configBasedSpriteManager != null)
                    {
                        ModLogger.Log("Updating sprite manager with new configuration...");
                        _configBasedSpriteManager.UpdateConfiguration(config);
                    }

                    // Simulate a menu refresh to reload sprites in-game
                    ModLogger.Log("Triggering sprite refresh in game...");
                    if (_inputSimulator != null)
                    {
                        ModLogger.Log("Calling SimulateMenuRefresh...");
                        bool refreshResult = _inputSimulator.SimulateMenuRefresh();
                        ModLogger.Log($"SimulateMenuRefresh returned: {refreshResult}");
                    }
                    else
                    {
                        ModLogger.LogWarning("InputSimulator is null - cannot refresh sprites!");
                    }
                }

                ModLogger.Log("Configuration window closed");
            }
            else
            {
                ModLogger.LogWarning("Configuration manager not initialized");
            }
        }
        catch (Exception ex)
        {
            ModLogger.LogError($"opening configuration UI: {ex.Message}");
        }
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
        _configBasedSpriteManager = new ConfigBasedSpriteManager(Path.GetDirectoryName(configPath), _configurationManager, _sourcePath);

        // Initialize theme manager if not already initialized
        if (_themeManager == null)
        {
            _themeManager = new ThemeManager(_sourcePath, _modPath);
        }

        // Load config and initialize story character themes
        var config = _configurationManager.LoadConfig();
        InitializeStoryCharacterThemes(config);
    }

    private void InitializeRegistry()
    {
        // Clear and auto-discover all story characters with attributes
        StoryCharacterRegistry.Clear();
        StoryCharacterRegistry.AutoDiscoverCharacters();

        ModLogger.Log($"Registry initialized with {StoryCharacterRegistry.GetAllCharacterNames().Count()} story characters");
    }

    private void InitializeStoryCharacterThemes(Config config)
    {
        // Initialize story character themes from config
        if (_themeManager != null)
        {
            var storyManager = _themeManager.GetStoryCharacterManager();
            if (storyManager != null)
            {
                // Use the old implementation methods for now
                storyManager.SetCurrentCloudTheme(config.Cloud);
                storyManager.SetCurrentAgriasTheme(config.Agrias);
                storyManager.SetCurrentOrlandeauTheme(config.Orlandeau);

                ModLogger.Log($"Applying initial Cloud theme: {config.Cloud}");
                ModLogger.Log($"Applying initial Agrias theme: {config.Agrias}");
                ModLogger.Log($"Applying initial Orlandeau theme: {config.Orlandeau}");
            }
        }
    }

    public ThemeManager? GetThemeManager()
    {
        return _themeManager;
    }

    public void ConfigurationUpdated(Config configuration)
    {
        // Initialize configuration manager if not already initialized
        if (_configurationManager == null)
        {
            var modPath = _modPath ?? Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? Path.GetTempPath();
            var defaultPath = Path.Combine(modPath, "Config.json");
            InitializeConfiguration(defaultPath);
        }

        // Update the configuration manager with the new config
        _configurationManager?.SaveConfig(configuration);

        // Apply the new configuration
        _configBasedSpriteManager?.ApplyConfiguration();

        // Update story character themes
        InitializeStoryCharacterThemes(configuration);

        ModLogger.Log("Configuration updated from Reloaded-II UI");
        ModLogger.Log($"Squire_Male set to: {configuration.Squire_Male}");
    }
} 