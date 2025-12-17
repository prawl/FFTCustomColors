using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using ColorMod.Registry;
using FFTColorMod.Configuration;
using FFTColorMod.Core;
using FFTColorMod.Services;
using FFTColorMod.Utilities;
using FFTColorMod.Interfaces;
using Reloaded.Mod.Interfaces;
using static FFTColorMod.Core.ColorModConstants;

namespace FFTColorMod;

/// <summary>
/// Your mod logic goes here.
/// </summary>
public class Mod : IMod, IConfigurable
{
    private IServiceContainer? _container;
    private IHotkeyHandler? _hotkeyHandler;
    private SpriteFileManager? _spriteFileManager;
    private ConfigBasedSpriteManager? _configBasedSpriteManager;
    private bool _isTestEnvironment = false;
    private ConfigurationManager? _configurationManager;
    private DynamicSpriteLoader? _dynamicSpriteLoader;
    private Process? _gameProcess;
    private string _currentColorScheme = DefaultTheme;
    private ColorSchemeCycler? _colorCycler;
    private IInputSimulator? _inputSimulator;
    private string _modPath;
    private string _sourcePath; // Path to git repo for theme sources
    public event Action? ConfigUIRequested;

    // Constructor that accepts ModContext and optional IInputSimulator (for testing)
    public Mod(ModContext context, IInputSimulator? inputSimulator = null, IHotkeyHandler? hotkeyHandler = null)
    {
        // Do minimal work in constructor - Reloaded will call Start() for initialization
        ModLogger.Log("Constructor called with ModContext");
        _inputSimulator = inputSimulator;
        _hotkeyHandler = hotkeyHandler;

        // If NullHotkeyHandler is provided, we're likely in a test environment
        _isTestEnvironment = hotkeyHandler is NullHotkeyHandler;

        // Try to auto-detect sprite variants from mod directory
        _modPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;

        // Set the source path to the git repo location (hardcoded for now, could be made configurable)
        _sourcePath = DevSourcePath;

        // Use source path for detecting themes (from git repo)
        string spritesPath = Path.Combine(_sourcePath, FFTIVCPath, DataPath, EnhancedPath, FFTPackPath, UnitPath);

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

    // Constructor for dependency injection
    public Mod(IServiceContainer container) : this(new ModContext())
    {
        _container = container;
    }

    public Config? GetConfiguration()
    {
        if (_configurationManager == null && _container != null)
        {
            _configurationManager = _container.Resolve<ConfigurationManager>();
        }
        return _configurationManager?.LoadConfig();
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

        // Initialize theme manager (themes will be applied after config is loaded)
        _themeManager = new ThemeManager(_sourcePath, _modPath);

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

        // Initialize and start hotkey handler (use provided or create default)
        if (_hotkeyHandler == null)
        {
            _hotkeyHandler = new HotkeyHandler(vkCode => _hotkeyService.ProcessHotkeyPress(vkCode));
        }
        _hotkeyHandler.StartMonitoring();

        ModLogger.Log("Loaded successfully!");
        ModLogger.Log("Press F1 to open the Configuration UI");
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
            .Where(p => p.PropertyType == typeof(string) &&
                       (p.Name.EndsWith("_Male") || p.Name.EndsWith("_Female")));

        foreach (var property in properties)
        {
            var value = property.GetValue(config);
            if (value is string colorScheme)
            {
                result[property.Name] = ConvertThemeNameToDisplayName(colorScheme);
            }
            else
            {
                result[property.Name] = "Original";
            }
        }
        return result;
    }

    /// <summary>
    /// Converts internal theme name (e.g., "lucavi", "corpse_brigade") to display name (e.g., "Lucavi", "Corpse Brigade")
    /// </summary>
    private string ConvertThemeNameToDisplayName(string themeName)
    {
        if (string.IsNullOrEmpty(themeName))
            return "Original";

        // Replace underscores with spaces and convert to title case
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
            themeName.Replace('_', ' ')
        );
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
        // Skip opening UI if we're in a test environment
        if (_isTestEnvironment)
        {
            ModLogger.Log("Skipping UI display in test environment");
            ConfigUIRequested?.Invoke();
            return;
        }

        Console.WriteLine("[FFT Color Mod] === OpenConfigurationUI called ===");
        try
        {
            Console.WriteLine($"[FFT Color Mod] Configuration manager initialized: {_configurationManager != null}");
            Console.WriteLine($"[FFT Color Mod] Mod path: {_modPath}");
            ModLogger.Log("=== OpenConfigurationUI called ===");
            ModLogger.Log($"Configuration manager initialized: {_configurationManager != null}");
            ModLogger.Log($"Mod path: {_modPath}");

            // Load current configuration
            if (_configurationManager != null)
            {
                var config = _configurationManager.LoadConfig();

                // Use the same User directory path that the mod is using
                var reloadedRoot = Directory.GetParent(Directory.GetParent(_modPath).FullName)?.FullName ?? _modPath;
                var configPath = Path.Combine(reloadedRoot, "User", "Mods", "ptyra.fft.colormod", "Config.json");

                // Pass the source path (git repo) as the mod path so it can find preview images
                var configForm = new Configuration.ConfigurationForm(config, configPath, _sourcePath);

                var result = configForm.ShowDialog();

                Console.WriteLine($"[FFT Color Mod] Dialog result: {result}");

                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    Console.WriteLine("[FFT Color Mod] Saving configuration...");
                    ModLogger.Log("Saving configuration...");
                    _configurationManager.SaveConfig(config);

                    // Update the config-based sprite manager with new configuration
                    if (_configBasedSpriteManager != null)
                    {
                        Console.WriteLine("[FFT Color Mod] Updating sprite manager with new configuration...");
                        ModLogger.Log("Updating sprite manager with new configuration...");
                        _configBasedSpriteManager.UpdateConfiguration(config);
                    }
                    else
                    {
                        Console.WriteLine("[FFT Color Mod] WARNING: _configBasedSpriteManager is null!");
                    }

                    // Simulate a menu refresh to reload sprites in-game
                    Console.WriteLine("[FFT Color Mod] Triggering sprite refresh in game...");
                    ModLogger.Log("Triggering sprite refresh in game...");
                    if (_inputSimulator != null)
                    {
                        Console.WriteLine("[FFT Color Mod] Calling SimulateMenuRefresh...");
                        ModLogger.Log("Calling SimulateMenuRefresh...");
                        bool refreshResult = _inputSimulator.SimulateMenuRefresh();
                        Console.WriteLine($"[FFT Color Mod] SimulateMenuRefresh returned: {refreshResult}");
                        ModLogger.Log($"SimulateMenuRefresh returned: {refreshResult}");
                    }
                    else
                    {
                        Console.WriteLine("[FFT Color Mod] WARNING: InputSimulator is null!");
                        ModLogger.LogWarning("InputSimulator is null - cannot refresh sprites!");
                    }
                }

                Console.WriteLine("[FFT Color Mod] Configuration window closed");
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

        // Initialize job class service with mod path
        JobClassServiceSingleton.Initialize(_modPath);
        var jobClassService = JobClassServiceSingleton.Instance;
        ModLogger.Log($"Loaded {jobClassService.GetAllJobClasses().Count()} generic job classes from JobClasses.json");
    }

    private void InitializeStoryCharacterThemes(Config config)
    {
        // Validate theme files exist
        var validationService = new ThemeValidationService(_modPath);
        var validationResults = validationService.ValidateConfiguration(config);
        validationService.LogValidationResults();

        // Initialize story character themes from config
        if (_themeManager != null)
        {
            var storyManager = _themeManager.GetStoryCharacterManager();
            if (storyManager != null)
            {
                // Story characters
                storyManager.SetCurrentTheme("Cloud", config.Cloud);
                storyManager.SetCurrentTheme("Agrias", config.Agrias);
                storyManager.SetCurrentTheme("Orlandeau", config.Orlandeau);
                storyManager.SetCurrentTheme("Mustadio", config.Mustadio);
                storyManager.SetCurrentTheme("Reis", config.Reis);
                storyManager.SetCurrentTheme("Rapha", config.Rapha);
                storyManager.SetCurrentTheme("Marach", config.Marach);
                storyManager.SetCurrentTheme("Beowulf", config.Beowulf);
                storyManager.SetCurrentTheme("Meliadoul", config.Meliadoul);

                // Log all themes for debugging
                ModLogger.Log($"Applying initial Cloud theme: {config.Cloud}");
                ModLogger.Log($"Applying initial Agrias theme: {config.Agrias}");
                ModLogger.Log($"Applying initial Orlandeau theme: {config.Orlandeau}");
                ModLogger.Log($"Applying initial Mustadio theme: {config.Mustadio}");
                ModLogger.Log($"Applying initial Reis theme: {config.Reis}");
                ModLogger.Log($"Applying initial Rapha theme: {config.Rapha}");
                ModLogger.Log($"Applying initial Marach theme: {config.Marach}");
                ModLogger.Log($"Applying initial Beowulf theme: {config.Beowulf}");
                ModLogger.Log($"Applying initial Meliadoul theme: {config.Meliadoul}");

                // Apply the themes AFTER they've been set from config
                _themeManager.ApplyInitialThemes();
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