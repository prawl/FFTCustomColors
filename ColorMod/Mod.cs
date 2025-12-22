using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Core.ModComponents;
using FFTColorCustomizer.Interfaces;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.Utilities;
using Reloaded.Mod.Interfaces;
using static FFTColorCustomizer.Core.ColorModConstants;

namespace FFTColorCustomizer;

/// <summary>
/// Thin orchestrator for FFT Color Mod - delegates to specialized components
/// </summary>
public class Mod : IMod, IConfigurable
{
    // Core components
    private Core.ModComponents.ModInitializer? _initializer;
    private ConfigurationCoordinator? _configCoordinator;
    private ThemeCoordinator? _themeCoordinator;
    private HotkeyManager? _hotkeyManager;

    // State
    private readonly string _modPath;
    private readonly string _sourcePath;
    private readonly bool _isTestEnvironment;
    private readonly IInputSimulator? _inputSimulator;
    private readonly IHotkeyHandler? _hotkeyHandler;
    private bool _hasStarted = false;

    // Events
    public event Action? ConfigUIRequested;

    // Properties
    public string ModId => Core.ColorModConstants.ModId;

    #region Constructors

    public Mod(ModContext context, IInputSimulator? inputSimulator = null, IHotkeyHandler? hotkeyHandler = null)
    {
        // Initialize ModLogger with ConsoleLogger
        ModLogger.Reset();
        ModLogger.Instance = new ConsoleLogger("[FFT Color Mod]");
        ModLogger.LogLevel = Interfaces.LogLevel.Debug; // DEBUG BUILD - Maximum verbosity
        ModLogger.Log("Mod constructor called - DEBUG BUILD v1.2.2-debug");

        _inputSimulator = inputSimulator;
        _hotkeyHandler = hotkeyHandler;
        _isTestEnvironment = hotkeyHandler is NullHotkeyHandler;

        // Initialize paths
        _modPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
            ?? Environment.CurrentDirectory;
        // In deployment, source and mod path are the same (sprites are in the mod directory)
        _sourcePath = _modPath;

        ModLogger.Log($"Mod initialized with path: {_modPath}");
        ModLogger.Log($"Assembly location: {System.Reflection.Assembly.GetExecutingAssembly().Location}");

        // Initialize components
        InitializeComponents();
    }

    public Mod()
    {
        // Initialize ModLogger with ConsoleLogger
        ModLogger.Reset();
        ModLogger.Instance = new ConsoleLogger("[FFT Color Mod]");
        ModLogger.LogLevel = Interfaces.LogLevel.Debug; // DEBUG BUILD - Maximum verbosity for troubleshooting

        ModLogger.Log("Default constructor called");

        // Initialize paths first
        _modPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
            ?? Environment.CurrentDirectory;
        // In deployment, source and mod path are the same (sprites are in the mod directory)
        _sourcePath = _modPath;

        ModLogger.Log($"Mod initialized with path: {_modPath}");
        ModLogger.Log($"Assembly location: {System.Reflection.Assembly.GetExecutingAssembly().Location}");

        // Set up production components
        _inputSimulator = new InputSimulator();
        _hotkeyHandler = new HotkeyHandler(ProcessHotkeyPress);
        _isTestEnvironment = false;

        ModLogger.LogDebug($"HotkeyHandler created: {_hotkeyHandler != null}");
        ModLogger.LogDebug($"InputSimulator created: {_inputSimulator != null}");

        // Initialize components
        InitializeComponents();

        // Initialize configuration immediately to ensure it's ready
        var configPath = GetUserConfigPath();
        InitializeConfiguration(configPath);

        // Since Start is not being called by Reloaded-II, call it manually
        ModLogger.LogDebug("Calling Start manually from constructor");
        Start(null);
    }

    public Mod(IServiceContainer container) : this(new ModContext())
    {
        // Initialize configuration immediately when using DI
        var configPath = GetUserConfigPath();
        InitializeConfiguration(configPath);
    }

    #endregion

    #region Component Initialization

    private void InitializeComponents()
    {
        try
        {
            ModLogger.Log("Initializing mod components");

            // Create initializer and set up core components
            _initializer = new Core.ModComponents.ModInitializer(_modPath, _isTestEnvironment);
            _initializer.InitializeRegistry();
            _initializer.InitializeColorSchemeCycler();

            // Initialize theme coordinator
            _themeCoordinator = new ThemeCoordinator(_sourcePath, _modPath);

            ModLogger.Log("Mod components initialized successfully");
        }
        catch (Exception ex)
        {
            ModLogger.LogError($"Failed to initialize components: {ex.Message}");
        }
    }

    public void InitializeConfiguration(string configPath)
    {
        try
        {
            ModLogger.Log($"Initializing configuration at: {configPath}");

            // Set the mod path for CharacterServiceSingleton to find StoryCharacters.json
            Services.CharacterServiceSingleton.SetModPath(_modPath);
            // Also initialize JobClassServiceSingleton with the mod path
            Services.JobClassServiceSingleton.Initialize(_modPath);

            // Initialize coordinators and managers - pass actual mod path to avoid path resolution issues
            _configCoordinator = new ConfigurationCoordinator(configPath, _modPath);

            // Get the theme manager from the coordinator (use the same instance everywhere)
            var themeManager = _themeCoordinator?.GetThemeManager();

            // Setup hotkey handling
            if (themeManager != null)
            {
                _hotkeyManager = new HotkeyManager(
                    _inputSimulator,
                    themeManager,
                    () => OpenConfigurationUI(),
                    () => _configCoordinator?.ResetToDefaults()
                );
            }

            // Apply initial configuration themes
            ApplyInitialConfiguration(themeManager);

            ModLogger.Log("Configuration initialized successfully");
        }
        catch (Exception ex)
        {
            ModLogger.LogError($"Failed to initialize configuration: {ex.Message}");
        }
    }

    private void ApplyInitialConfiguration(ThemeManager? themeManager)
    {
        if (_configCoordinator == null || themeManager == null) return;

        var config = _configCoordinator.GetConfiguration();
        if (config != null)
        {
            _initializer?.InitializeStoryCharacterThemes(config, themeManager);
            // Don't apply themes here - let the Start method handle it with proper delay
            // This prevents duplicate calls to ApplyInitialThemes

            // Note: Generic job themes will be applied in Start method after delay
        }
    }

    #endregion

    #region IMod Implementation

    public void Start(IModLoader? modLoader)
    {
        ModLogger.Log("Start method called");

        // Prevent duplicate initialization
        if (_hasStarted)
        {
            ModLogger.LogDebug("Start already called, skipping duplicate initialization");
            return;
        }
        _hasStarted = true;

        ModLogger.LogDebug($"_hotkeyHandler exists: {_hotkeyHandler != null}");
        ModLogger.LogDebug($"_inputSimulator exists: {_inputSimulator != null}");
        ModLogger.LogDebug($"_configCoordinator exists: {_configCoordinator != null}");

        // Ensure configuration is initialized
        EnsureConfigurationInitialized();
        ModLogger.LogDebug($"After EnsureConfig - _configCoordinator exists: {_configCoordinator != null}");
        ModLogger.LogDebug($"After EnsureConfig - _hotkeyManager exists: {_hotkeyManager != null}");

        // Apply themes - immediately in test environment, with delay in production
        if (_configCoordinator != null && _themeCoordinator != null)
        {
            if (_isTestEnvironment)
            {
                // In test environment, apply themes immediately (synchronously)
                ModLogger.Log("Applying initial themes immediately (test environment)...");
                var config = _configCoordinator.GetConfiguration();
                if (config != null)
                {
                    var themeManager = _themeCoordinator.GetThemeManager();
                    if (themeManager != null)
                    {
                        _initializer?.InitializeStoryCharacterThemes(config, themeManager);
                        themeManager.ApplyInitialThemes();

                        // CRITICAL: Also apply generic job themes
                        _configCoordinator?.ApplyConfiguration();

                        ModLogger.Log("Initial themes applied successfully (test environment)");
                    }
                }
            }
            else
            {
                // In production, apply themes after a delay to ensure mod loader is ready
                ModLogger.Log("Scheduling initial theme application after delay...");
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        // Wait for mod loader to be fully initialized
                        await System.Threading.Tasks.Task.Delay(500);

                        ModLogger.Log("Applying initial themes after delay...");
                        var config = _configCoordinator.GetConfiguration();
                        if (config != null)
                        {
                            ModLogger.Log($"Config loaded, applying themes for {config.GetType().GetProperties().Length} properties");
                            var themeManager = _themeCoordinator.GetThemeManager();
                            if (themeManager != null)
                            {
                                _initializer?.InitializeStoryCharacterThemes(config, themeManager);
                                themeManager.ApplyInitialThemes();

                                // CRITICAL: Also apply generic job themes after delay
                                _configCoordinator?.ApplyConfiguration();

                                ModLogger.Log("Initial themes applied successfully after delay");
                            }
                            else
                            {
                                ModLogger.LogWarning("ThemeManager is null after delay, cannot apply themes");
                            }
                        }
                        else
                        {
                            ModLogger.LogWarning("Configuration is null after delay, cannot apply themes");
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLogger.LogError($"Failed to apply initial themes after delay: {ex.Message}");
                    }
                });
            }
        }
        else
        {
            ModLogger.LogWarning($"Cannot schedule theme application: configCoordinator={_configCoordinator != null}, themeCoordinator={_themeCoordinator != null}");
        }

        // Start hotkey monitoring
        if (_hotkeyHandler != null)
        {
            ModLogger.Log("Starting hotkey monitoring");
            _hotkeyHandler.StartMonitoring();
        }
        else
        {
            ModLogger.LogError("HotkeyHandler is null, cannot start monitoring!");
        }
    }

    private void EnsureConfigurationInitialized()
    {
        if (_configCoordinator == null)
        {
            ModLogger.Log("Configuration coordinator is null, initializing...");
            var configPath = GetUserConfigPath();
            InitializeConfiguration(configPath);
        }
        else
        {
            ModLogger.LogDebug("Configuration coordinator already initialized");
        }
    }

    private string GetUserConfigPath()
    {
        // Navigate from Mods/FFTColorCustomizer to User/Mods/paxtrick.fft.colorcustomizer
        var parent = Directory.GetParent(_modPath);
        if (parent != null)
        {
            var grandParent = Directory.GetParent(parent.FullName);
            if (grandParent != null)
            {
                var reloadedRoot = grandParent.FullName;
                var userConfigPath = Path.Combine(reloadedRoot, "User", "Mods", "paxtrick.fft.colorcustomizer", ConfigFileName);

                ModLogger.LogDebug($"Looking for user config at: {userConfigPath}");

                // Use User config if it exists
                if (File.Exists(userConfigPath))
                {
                    ModLogger.Log($"Using user config: {userConfigPath}");
                    return userConfigPath;
                }
                else
                {
                    ModLogger.LogDebug("User config not found, falling back to mod config");
                }
            }
        }

        // Fallback to mod directory config
        var fallbackPath = Path.Combine(_modPath, ConfigFileName);
        ModLogger.Log($"Using fallback config: {fallbackPath}");
        return fallbackPath;
    }

    public void Suspend() => ModLogger.Log("Mod suspended");
    public void Resume() => ModLogger.Log("Mod resumed");
    public void Unload() => ModLogger.Log("Mod unloading");

    public bool CanUnload() => false;
    public bool CanSuspend() => false;
    public Action Disposing { get; } = () => { };

    #endregion

    #region IConfigurable Implementation

    public string? ConfigName { get; set; } = "FFT Color Mod Configuration";

    public Config? GetConfiguration()
        => _configCoordinator?.GetConfiguration();

    public void ConfigurationUpdated(Config configuration)
    {
        ModLogger.Log("Configuration updated from Reloaded-II UI");
        _configCoordinator?.UpdateConfiguration(configuration);
        ApplyConfigurationThemes(configuration);
    }

    private void ApplyConfigurationThemes(Config? configuration)
    {
        if (configuration == null || _themeCoordinator == null) return;

        var themeManager = _themeCoordinator.GetThemeManager();
        if (themeManager != null)
        {
            _initializer?.InitializeStoryCharacterThemes(configuration, themeManager);
            // Apply themes when configuration is updated externally (from Reloaded-II UI)
            // This ensures the sprite files are actually copied to the correct locations
            themeManager.ApplyInitialThemes();
        }

        // CRITICAL: Also apply generic job themes (knights, monks, archers, etc.)
        // This was missing and why generic characters didn't get themed on startup
        _configCoordinator?.ApplyConfiguration();
    }

    public Action Save => () =>
    {
        _configCoordinator?.SaveConfiguration();
        ApplyConfigurationThemes(GetConfiguration());
    };

    #endregion

    #region Configuration Operations

    public void SetJobColor(string jobProperty, string colorScheme)
        => _configCoordinator?.SetJobColor(jobProperty, colorScheme);

    public string GetJobColor(string jobProperty)
        => _configCoordinator?.GetJobColor(jobProperty) ?? DefaultTheme;

    public Dictionary<string, string> GetAllJobColors()
        => _configCoordinator?.GetAllJobColors() ?? new Dictionary<string, string>();

    public void ResetAllColors()
    {
        _configCoordinator?.ResetToDefaults();
    }

    public bool HasConfigurationManager()
        => _configCoordinator?.HasConfigurationManager() ?? false;

    #endregion

    #region Theme Operations

    public void SetColorScheme(string scheme)
        => _themeCoordinator?.SetColorScheme(scheme);

    public string GetCurrentColorScheme()
        => _themeCoordinator?.GetCurrentColorScheme() ?? DefaultTheme;

    public string InterceptFilePath(string originalPath)
    {
        // Add diagnostic logging to debug why themes aren't loading
        ModLogger.LogDebug($"[INTERCEPT] Called with path: {originalPath}");

        // Ensure configuration and theme coordinator are initialized
        if (_themeCoordinator == null)
        {
            ModLogger.LogError("[INTERCEPT] ThemeCoordinator is null! Themes will not load!");
            return originalPath;
        }

        // First, try to use per-job configuration if available
        // This fixes the hot reload issue where F1 config changes weren't being applied
        if (_configCoordinator != null)
        {
            var fileName = Path.GetFileName(originalPath);

            // Check if this is a job sprite that might have a per-job configuration
            if (fileName.Contains("battle_") && fileName.EndsWith("_spr.bin"))
            {
                var jobColor = GetJobColorForSprite(fileName);
                if (!string.IsNullOrEmpty(jobColor) && jobColor != "original")
                {
                    // Build the themed sprite path
                    var themedDir = Path.Combine(_modPath, FFTIVCPath, DataPath, EnhancedPath,
                        FFTPackPath, UnitPath, $"{SpritesPrefix}{jobColor}");
                    var themedPath = Path.Combine(themedDir, fileName);

                    // Return themed path if it exists
                    if (File.Exists(themedPath))
                    {
                        ModLogger.LogSuccess($"[INTERCEPT] Per-job redirect: {Path.GetFileName(originalPath)} -> {jobColor}/{Path.GetFileName(themedPath)}");
                        return themedPath;
                    }
                }
            }
        }

        // Fall back to the original global theme behavior
        var result = _themeCoordinator.InterceptFilePath(originalPath);
        if (result != originalPath)
        {
            ModLogger.LogSuccess($"[INTERCEPT] Global redirect: {Path.GetFileName(originalPath)} -> {Path.GetFileName(result)}");
            Console.WriteLine($"[FFT Color Mod] Intercepted: {Path.GetFileName(originalPath)} -> {Path.GetFileName(result)}");
        }
        else
        {
            // Log why interception didn't happen
            if (originalPath.Contains(".bin") && originalPath.Contains("battle_"))
            {
                ModLogger.LogDebug($"[INTERCEPT] No redirect for sprite: {Path.GetFileName(originalPath)}");
            }
        }
        return result;
    }

    private string GetJobColorForSprite(string spriteName)
    {
        // Use JobClassService to get the job class from sprite name
        // This uses the data from JobClasses.json instead of hardcoding
        var jobClassService = Services.JobClassServiceSingleton.Instance;
        if (jobClassService == null)
        {
            ModLogger.LogDebug($"JobClassService not initialized for sprite: {spriteName}");
            return null;
        }

        var jobClass = jobClassService.GetJobClassBySpriteName(spriteName);
        if (jobClass != null)
        {
            // jobClass.Name is the property name (e.g., "Knight_Male")
            return GetJobColor(jobClass.Name);
        }

        return null;
    }

    public ThemeManager? GetThemeManager()
        => _themeCoordinator?.GetThemeManager();

    #endregion

    #region Hotkey Handling

    public void ProcessHotkeyPress(int vkCode)
    {
        ModLogger.LogDebug($"ProcessHotkeyPress called with vkCode: 0x{vkCode:X}");
        // Ensure configuration is initialized before processing hotkeys
        EnsureConfigurationInitialized();
        ModLogger.LogDebug($"_hotkeyManager exists: {_hotkeyManager != null}");
        _hotkeyManager?.ProcessHotkeyPress(vkCode);
    }

    #endregion

    #region UI Operations

    protected virtual void OpenConfigurationUI()
    {
        ModLogger.Log("Opening configuration UI");
        ConfigUIRequested?.Invoke();

        // Only open actual UI if not in test environment
        if (!_isTestEnvironment)
        {
            // Delegate UI handling to coordinator
            _configCoordinator?.OpenConfigurationUI((config) => ConfigurationUpdated(config));
        }
    }

    public bool IsConfigUIRequested()
    {
        return ConfigUIRequested != null && ConfigUIRequested.GetInvocationList().Length > 0;
    }

    #endregion

    #region Internal Methods (for testing)

    internal bool IsJobSprite(string fileName)
        => _themeCoordinator?.IsJobSprite(fileName) ?? false;

    #endregion

    public bool SupportsPerCharacterColors() => true;
}
