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
/// Thin orchestrator for FFT Color Mod, delegates to specialized components.
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
        _inputSimulator = inputSimulator;
        _hotkeyHandler = hotkeyHandler;
        _isTestEnvironment = hotkeyHandler is NullHotkeyHandler;

        // Initialize paths
        _modPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
            ?? Environment.CurrentDirectory;
        // In deployment, source and mod path are the same (sprites are in the mod directory)
        _sourcePath = _modPath;
        string logs = Path.Combine(_modPath, "logs");
        Directory.CreateDirectory(logs);
        ModLogger.Init(logs, "[Color Customizer]", "live_log");
        ModLogger.Event(LogVerb.Startup, "Color Customizer is loaded; press F1 in game for the theme editor");
        ModLogger.Debug(LogVerb.Trace, $"Mod initialized with path: {_modPath}");
        ModLogger.Debug(LogVerb.Trace, $"Assembly location: {System.Reflection.Assembly.GetExecutingAssembly().Location}");

        // Initialize components
        InitializeComponents();
    }

    public Mod()
    {
        // Initialize paths first
        _modPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
            ?? Environment.CurrentDirectory;
        // In deployment, source and mod path are the same (sprites are in the mod directory)
        _sourcePath = _modPath;
        string logs = Path.Combine(_modPath, "logs");
        Directory.CreateDirectory(logs);
        ModLogger.Init(logs, "[Color Customizer]", "live_log");
        ModLogger.Debug(LogVerb.Trace, $"Mod initialized with path: {_modPath}");
        ModLogger.Debug(LogVerb.Trace, $"Assembly location: {System.Reflection.Assembly.GetExecutingAssembly().Location}");

        // Set up production components
        _inputSimulator = new InputSimulator();
        _hotkeyHandler = new HotkeyHandler(ProcessHotkeyPress);
        _isTestEnvironment = false;

        ModLogger.Debug(LogVerb.Startup, $"HotkeyHandler created: {_hotkeyHandler != null}");
        ModLogger.Debug(LogVerb.Startup, $"InputSimulator created: {_inputSimulator != null}");

        // Initialize components
        InitializeComponents();

        // Initialize configuration immediately to ensure it's ready
        var configPath = GetUserConfigPath();
        InitializeConfiguration(configPath);

        // Since Start is not being called by Reloaded-II, call it manually
        ModLogger.Debug(LogVerb.Startup, "Calling Start manually from constructor");
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
            ModLogger.Debug(LogVerb.Startup, "Initializing mod components");

            // Create initializer and set up core components
            _initializer = new Core.ModComponents.ModInitializer(_modPath, _isTestEnvironment);
            _initializer.InitializeRegistry();
            _initializer.InitializeColorSchemeCycler();

            // Initialize theme coordinator
            _themeCoordinator = new ThemeCoordinator(_sourcePath, _modPath);

            ModLogger.Debug(LogVerb.Startup, "Mod components initialized successfully");
        }
        catch (Exception ex)
        {
            ModLogger.Error(LogVerb.Startup, $"Failed to initialize components: {ex.Message}");
        }
    }

    public void InitializeConfiguration(string configPath)
    {
        try
        {
            ModLogger.Debug(LogVerb.Config, $"Initializing configuration at: {configPath}");

            // Set the mod path for CharacterServiceSingleton to find StoryCharacters.json
            Services.CharacterServiceSingleton.SetModPath(_modPath);
            // Also initialize JobClassServiceSingleton with the mod path
            Services.JobClassServiceSingleton.Initialize(_modPath);

            // Initialize coordinators and managers, pass actual mod path to avoid path resolution issues
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

            ModLogger.Debug(LogVerb.Config, "Configuration initialized successfully");
        }
        catch (Exception ex)
        {
            ModLogger.Error(LogVerb.Config, $"Failed to initialize configuration: {ex.Message}");
        }
    }

    private void ApplyInitialConfiguration(ThemeManager? themeManager)
    {
        if (_configCoordinator == null || themeManager == null) return;

        var config = _configCoordinator.GetConfiguration();
        if (config != null)
        {
            _initializer?.InitializeStoryCharacterThemes(config, themeManager);
            // Don't apply themes here, let the Start method handle it with proper delay
            // This prevents duplicate calls to ApplyInitialThemes

            // Generic job themes will be applied in Start method after delay
        }
    }

    #endregion

    #region IMod Implementation

    public void Start(IModLoader? modLoader)
    {
        ModLogger.Debug(LogVerb.Startup, "Start method called");

        // Prevent duplicate initialization
        if (_hasStarted)
        {
            ModLogger.Debug(LogVerb.Startup, "Start already called, skipping duplicate initialization");
            return;
        }
        _hasStarted = true;

        ModLogger.Debug(LogVerb.Startup, $"_hotkeyHandler exists: {_hotkeyHandler != null}");
        ModLogger.Debug(LogVerb.Startup, $"_inputSimulator exists: {_inputSimulator != null}");
        ModLogger.Debug(LogVerb.Startup, $"_configCoordinator exists: {_configCoordinator != null}");

        // Ensure configuration is initialized
        EnsureConfigurationInitialized();
        ModLogger.Debug(LogVerb.Startup, $"After EnsureConfig - _configCoordinator exists: {_configCoordinator != null}");
        ModLogger.Debug(LogVerb.Startup, $"After EnsureConfig - _hotkeyManager exists: {_hotkeyManager != null}");

        // Apply themes, immediately in test environment, with delay in production
        if (_configCoordinator != null && _themeCoordinator != null)
        {
            if (_isTestEnvironment)
            {
                // In test environment, apply themes immediately (synchronously)
                ModLogger.Debug(LogVerb.Theme, "Applying initial themes immediately (test environment)...");
                var config = _configCoordinator.GetConfiguration();
                if (config != null)
                {
                    var themeManager = _themeCoordinator.GetThemeManager();
                    if (themeManager != null)
                    {
                        _initializer?.InitializeStoryCharacterThemes(config, themeManager);
                        themeManager.ApplyInitialThemes();

                        // Also apply generic job themes
                        _configCoordinator?.ApplyConfiguration();

                        ModLogger.Debug(LogVerb.Theme, "Initial themes applied successfully (test environment)");
                    }
                }
            }
            else
            {
                // In production, apply themes after a delay to ensure mod loader is ready
                ModLogger.Debug(LogVerb.Theme, "Scheduling initial theme application after delay...");
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        // Wait for mod loader to be fully initialized
                        await System.Threading.Tasks.Task.Delay(500);

                        ModLogger.Debug(LogVerb.Theme, "Applying initial themes after delay...");
                        var config = _configCoordinator.GetConfiguration();
                        if (config != null)
                        {
                            ModLogger.Debug(LogVerb.Theme, $"Config loaded, applying themes for {config.GetType().GetProperties().Length} properties");
                            var themeManager = _themeCoordinator.GetThemeManager();
                            if (themeManager != null)
                            {
                                _initializer?.InitializeStoryCharacterThemes(config, themeManager);
                                themeManager.ApplyInitialThemes();

                                // Also apply generic job themes after delay
                                _configCoordinator?.ApplyConfiguration();

                                ModLogger.Debug(LogVerb.Theme, "Initial themes applied successfully after delay");
                            }
                            else
                            {
                                ModLogger.Warn(LogVerb.Theme, "ThemeManager is null after delay, cannot apply themes");
                            }
                        }
                        else
                        {
                            ModLogger.Warn(LogVerb.Theme, "Configuration is null after delay, cannot apply themes");
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Error(LogVerb.Theme, $"Failed to apply initial themes after delay: {ex.Message}");
                    }
                });
            }
        }
        else
        {
            ModLogger.Warn(LogVerb.Theme, $"Cannot schedule theme application: configCoordinator={_configCoordinator != null}, themeCoordinator={_themeCoordinator != null}");
        }

        // Start hotkey monitoring
        if (_hotkeyHandler != null)
        {
            ModLogger.Debug(LogVerb.Startup, "Starting hotkey monitoring");
            _hotkeyHandler.StartMonitoring();
        }
        else
        {
            ModLogger.Error(LogVerb.Startup, "HotkeyHandler is null, cannot start monitoring!");
        }
    }

    private void EnsureConfigurationInitialized()
    {
        if (_configCoordinator == null)
        {
            ModLogger.Debug(LogVerb.Startup, "Configuration coordinator is null, initializing...");
            var configPath = GetUserConfigPath();
            InitializeConfiguration(configPath);
        }
        else
        {
            ModLogger.Debug(LogVerb.Startup, "Configuration coordinator already initialized");
        }
    }

    private string GetUserConfigPath()
    {
        // every flow (F1 and the launcher Configure button) must converge on the
        // Reloaded User config path; the old exists-check fallback let the two flows write
        // different files, silently reverting selections. Migration of a legacy mod-folder
        // config happens inside the resolver.
        var configPath = Configuration.ConfigPathResolver.ResolveWithMigration(_modPath, ModNamespace, ConfigFileName);
        ModLogger.Debug(LogVerb.Config, $"Using config: {configPath}");
        return configPath;
    }

    public void Suspend() => ModLogger.Debug(LogVerb.Startup, "Mod suspended");
    public void Resume() => ModLogger.Debug(LogVerb.Startup, "Mod resumed");
    public void Unload() => ModLogger.Debug(LogVerb.Startup, "Mod unloading");

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
        ModLogger.Event(LogVerb.Config, "Configuration updated from Reloaded-II UI");
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

        // Also apply generic job themes (knights, monks, archers, etc.)
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
        ModLogger.Debug(LogVerb.Sprite, $"Called with path: {originalPath}");

        // Ensure configuration and theme coordinator are initialized
        if (_themeCoordinator == null)
        {
            ModLogger.Error(LogVerb.Sprite, "ThemeCoordinator is null! Themes will not load!");
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
                        ModLogger.Debug(LogVerb.Sprite, $"Per-job redirect: {Path.GetFileName(originalPath)} -> {jobColor}/{Path.GetFileName(themedPath)}");
                        return themedPath;
                    }
                }
            }
        }

        // Fall back to the original global theme behavior
        var result = _themeCoordinator.InterceptFilePath(originalPath);
        if (result != originalPath)
        {
            ModLogger.Debug(LogVerb.Sprite, $"Global redirect: {Path.GetFileName(originalPath)} -> {Path.GetFileName(result)}");
        }
        else
        {
            // Log why interception didn't happen
            if (originalPath.Contains(".bin") && originalPath.Contains("battle_"))
            {
                ModLogger.Debug(LogVerb.Sprite, $"No redirect for sprite: {Path.GetFileName(originalPath)}");
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
            ModLogger.Debug(LogVerb.Sprite, $"JobClassService not initialized for sprite: {spriteName}");
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
        ModLogger.Debug(LogVerb.Startup, $"ProcessHotkeyPress called with vkCode: 0x{vkCode:X}");
        // Ensure configuration is initialized before processing hotkeys
        EnsureConfigurationInitialized();
        ModLogger.Debug(LogVerb.Startup, $"_hotkeyManager exists: {_hotkeyManager != null}");
        _hotkeyManager?.ProcessHotkeyPress(vkCode);
    }

    #endregion

    #region UI Operations

    protected virtual void OpenConfigurationUI()
    {
        ModLogger.Event(LogVerb.Ui, "Opening configuration UI");
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
