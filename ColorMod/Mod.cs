using System;
using System.Collections.Generic;
using System.IO;
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
        ModLogger.LogLevel = Interfaces.LogLevel.Info;
        ModLogger.Log("Mod constructor called");

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
        ModLogger.LogLevel = Interfaces.LogLevel.Info; // Set to Info for normal operation (Debug for verbose)

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

            // Initialize coordinators and managers
            _configCoordinator = new ConfigurationCoordinator(configPath);

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
            themeManager.ApplyInitialThemes();
        }
    }

    #endregion

    #region IMod Implementation

    public void Start(IModLoader? modLoader)
    {
        ModLogger.Log("Start method called");
        ModLogger.LogDebug($"_hotkeyHandler exists: {_hotkeyHandler != null}");
        ModLogger.LogDebug($"_inputSimulator exists: {_inputSimulator != null}");
        ModLogger.LogDebug($"_configCoordinator exists: {_configCoordinator != null}");

        // Ensure configuration is initialized
        EnsureConfigurationInitialized();
        ModLogger.LogDebug($"After EnsureConfig - _configCoordinator exists: {_configCoordinator != null}");
        ModLogger.LogDebug($"After EnsureConfig - _hotkeyManager exists: {_hotkeyManager != null}");

        // Apply initial themes from configuration
        var configuration = GetConfiguration();
        if (configuration != null)
        {
            ApplyConfigurationThemes(configuration);
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
            var configPath = GetUserConfigPath();
            InitializeConfiguration(configPath);
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
            // Don't call ApplyInitialThemes here - it's already called in ApplyInitialConfiguration during startup
        }
    }

    public Action Save => () => _configCoordinator?.SaveConfiguration();

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

        var result = _themeCoordinator.InterceptFilePath(originalPath);
        if (result != originalPath)
        {
            ModLogger.LogSuccess($"[INTERCEPT] Redirected: {Path.GetFileName(originalPath)} -> {Path.GetFileName(result)}");
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
