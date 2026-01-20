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
/// Thin orchestrator for FFT Color Mod - delegates to specialized components.
/// This class is the entry point loaded by Reloaded-II mod loader.
/// </summary>
public class Mod : IMod, IConfigurable
{
    // Core components (managed by ModBootstrapper)
    private ModBootstrapper? _bootstrapper;
    private FileInterceptor? _fileInterceptor;
    private IHotkeyHandler? _hotkeyHandler;

    // State
    private bool _hasStarted = false;

    // Events
    public event Action? ConfigUIRequested;

    // Properties
    public string ModId => ColorModConstants.ModId;

    #region Constructors

    /// <summary>
    /// Test constructor with dependency injection
    /// </summary>
    public Mod(ModContext context, IInputSimulator? inputSimulator = null, IHotkeyHandler? hotkeyHandler = null)
    {
        InitializeLogger();
        ModLogger.Log("Mod constructor called (test mode) - DEBUG BUILD v2.0.2-debug");

        _bootstrapper = ModBootstrapper.CreateForTesting(context, inputSimulator, hotkeyHandler);
        _hotkeyHandler = hotkeyHandler;

        LogAssemblyInfo();
        InitializeFromBootstrapper();
    }

    /// <summary>
    /// Default constructor for Reloaded-II mod loader
    /// </summary>
    public Mod()
    {
        InitializeLogger();
        ModLogger.Log("Default constructor called");

        _bootstrapper = ModBootstrapper.CreateForProduction();
        _hotkeyHandler = new HotkeyHandler(ProcessHotkeyPress);

        LogAssemblyInfo();
        InitializeFromBootstrapper();

        // Initialize and start immediately for production
        var configPath = _bootstrapper.ResolveUserConfigPath();
        InitializeConfiguration(configPath);

        ModLogger.LogDebug("Calling Start manually from constructor");
        Start(null);
    }

    /// <summary>
    /// DI constructor
    /// </summary>
    public Mod(IServiceContainer container) : this(new ModContext())
    {
        var configPath = _bootstrapper!.ResolveUserConfigPath();
        InitializeConfiguration(configPath);
    }

    #endregion

    #region Initialization Helpers

    private static void InitializeLogger()
    {
        ModLogger.Reset();
        ModLogger.Instance = new ConsoleLogger("[FFT Color Mod]");
        ModLogger.LogLevel = Interfaces.LogLevel.Debug;
    }

    private void LogAssemblyInfo()
    {
        ModLogger.Log($"Mod initialized with path: {_bootstrapper?.ModPath}");
        ModLogger.Log($"Assembly location: {System.Reflection.Assembly.GetExecutingAssembly().Location}");
    }

    private void InitializeFromBootstrapper()
    {
        _bootstrapper?.InitializeCoreComponents();
    }

    #endregion

    #region Configuration

    public void InitializeConfiguration(string configPath)
    {
        try
        {
            _bootstrapper?.InitializeConfiguration(configPath);

            // Initialize hotkey handling
            var themeManager = _bootstrapper?.ThemeCoordinator?.GetThemeManager();
            if (themeManager != null)
            {
                _bootstrapper?.InitializeHotkeys(
                    ProcessHotkeyPress,
                    OpenConfigurationUI,
                    () => _bootstrapper?.ConfigCoordinator?.ResetToDefaults()
                );
            }

            // Create file interceptor
            if (_bootstrapper?.ThemeCoordinator != null && _bootstrapper?.ConfigCoordinator != null)
            {
                _fileInterceptor = new FileInterceptor(
                    _bootstrapper.ModPath,
                    _bootstrapper.ThemeCoordinator,
                    spriteName => _bootstrapper.ConfigCoordinator.GetJobColorForSprite(spriteName)
                );
            }

            // Apply initial configuration themes
            var config = _bootstrapper?.ConfigCoordinator?.GetConfiguration();
            if (config != null)
            {
                _bootstrapper?.ApplyInitialStoryCharacterThemes(config);
            }

            ModLogger.Log("Configuration initialized successfully");
        }
        catch (Exception ex)
        {
            ModLogger.LogError($"Failed to initialize configuration: {ex.Message}");
        }
    }

    #endregion

    #region IMod Implementation

    public void Start(IModLoader? modLoader)
    {
        ModLogger.Log("Start method called");

        if (_hasStarted)
        {
            ModLogger.LogDebug("Start already called, skipping duplicate initialization");
            return;
        }
        _hasStarted = true;

        EnsureConfigurationInitialized();
        ApplyThemesBasedOnEnvironment();
        StartHotkeyMonitoring();
    }

    private void EnsureConfigurationInitialized()
    {
        if (_bootstrapper?.ConfigCoordinator == null)
        {
            ModLogger.Log("Configuration coordinator is null, initializing...");
            var configPath = _bootstrapper?.ResolveUserConfigPath() ?? Path.Combine(Environment.CurrentDirectory, ConfigFileName);
            InitializeConfiguration(configPath);
        }
    }

    private void ApplyThemesBasedOnEnvironment()
    {
        var configCoordinator = _bootstrapper?.ConfigCoordinator;
        var themeCoordinator = _bootstrapper?.ThemeCoordinator;

        if (configCoordinator == null || themeCoordinator == null)
        {
            ModLogger.LogWarning("Cannot apply themes: coordinators not initialized");
            return;
        }

        if (_bootstrapper?.IsTestEnvironment == true)
        {
            ApplyThemesImmediately(configCoordinator, themeCoordinator);
        }
        else
        {
            ApplyThemesWithDelay(configCoordinator, themeCoordinator);
        }
    }

    private void ApplyThemesImmediately(ConfigurationCoordinator configCoordinator, ThemeCoordinator themeCoordinator)
    {
        ModLogger.Log("Applying initial themes immediately (test environment)...");

        var config = configCoordinator.GetConfiguration();
        if (config != null)
        {
            var themeManager = themeCoordinator.GetThemeManager();
            if (themeManager != null)
            {
                _bootstrapper?.Initializer?.InitializeStoryCharacterThemes(config, themeManager);
                themeManager.ApplyInitialThemes();
                configCoordinator.ApplyConfiguration();
                ModLogger.Log("Initial themes applied successfully (test environment)");
            }
        }
    }

    private void ApplyThemesWithDelay(ConfigurationCoordinator configCoordinator, ThemeCoordinator themeCoordinator)
    {
        ModLogger.Log("Scheduling initial theme application after delay...");

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500);
                ModLogger.Log("Applying initial themes after delay...");

                var config = configCoordinator.GetConfiguration();
                if (config == null)
                {
                    ModLogger.LogWarning("Configuration is null after delay");
                    return;
                }

                var themeManager = themeCoordinator.GetThemeManager();
                if (themeManager == null)
                {
                    ModLogger.LogWarning("ThemeManager is null after delay");
                    return;
                }

                _bootstrapper?.Initializer?.InitializeStoryCharacterThemes(config, themeManager);
                themeManager.ApplyInitialThemes();
                configCoordinator.ApplyConfiguration();
                ModLogger.Log("Initial themes applied successfully after delay");
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"Failed to apply initial themes after delay: {ex.Message}");
            }
        });
    }

    private void StartHotkeyMonitoring()
    {
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

    public void Suspend() => ModLogger.Log("Mod suspended");
    public void Resume() => ModLogger.Log("Mod resumed");
    public void Unload() => ModLogger.Log("Mod unloading");

    public bool CanUnload() => false;
    public bool CanSuspend() => false;
    public Action Disposing { get; } = () => { };

    #endregion

    #region IConfigurable Implementation

    public string? ConfigName { get; set; } = "FFT Color Mod Configuration";

    public Config? GetConfiguration() => _bootstrapper?.ConfigCoordinator?.GetConfiguration();

    public void ConfigurationUpdated(Config configuration)
    {
        ModLogger.Log("Configuration updated from Reloaded-II UI");
        _bootstrapper?.ConfigCoordinator?.UpdateConfiguration(configuration);
        ApplyConfigurationThemes(configuration);
    }

    private void ApplyConfigurationThemes(Config? configuration)
    {
        if (configuration == null || _bootstrapper?.ThemeCoordinator == null) return;

        var themeManager = _bootstrapper.ThemeCoordinator.GetThemeManager();
        if (themeManager != null)
        {
            _bootstrapper.Initializer?.InitializeStoryCharacterThemes(configuration, themeManager);
            themeManager.ApplyInitialThemes();
        }

        _bootstrapper.ConfigCoordinator?.ApplyConfiguration();
    }

    public Action Save => () =>
    {
        _bootstrapper?.ConfigCoordinator?.SaveConfiguration();
        ApplyConfigurationThemes(GetConfiguration());
    };

    #endregion

    #region Public API

    public void SetJobColor(string jobProperty, string colorScheme)
        => _bootstrapper?.ConfigCoordinator?.SetJobColor(jobProperty, colorScheme);

    public string GetJobColor(string jobProperty)
        => _bootstrapper?.ConfigCoordinator?.GetJobColor(jobProperty) ?? DefaultTheme;

    public Dictionary<string, string> GetAllJobColors()
        => _bootstrapper?.ConfigCoordinator?.GetAllJobColors() ?? new Dictionary<string, string>();

    public void ResetAllColors() => _bootstrapper?.ConfigCoordinator?.ResetToDefaults();

    public bool HasConfigurationManager() => _bootstrapper?.ConfigCoordinator?.HasConfigurationManager() ?? false;

    public void SetColorScheme(string scheme) => _bootstrapper?.ThemeCoordinator?.SetColorScheme(scheme);

    public string GetCurrentColorScheme() => _bootstrapper?.ThemeCoordinator?.GetCurrentColorScheme() ?? DefaultTheme;

    public string InterceptFilePath(string originalPath)
    {
        if (_fileInterceptor == null)
        {
            ModLogger.LogError("[INTERCEPT] FileInterceptor is null!");
            return originalPath;
        }
        return _fileInterceptor.InterceptFilePath(originalPath);
    }

    public ThemeManager? GetThemeManager() => _bootstrapper?.ThemeCoordinator?.GetThemeManager();

    #endregion

    #region Hotkey Handling

    public void ProcessHotkeyPress(int vkCode)
    {
        ModLogger.LogDebug($"ProcessHotkeyPress called with vkCode: 0x{vkCode:X}");
        EnsureConfigurationInitialized();
        _bootstrapper?.HotkeyManager?.ProcessHotkeyPress(vkCode);
    }

    #endregion

    #region UI Operations

    protected virtual void OpenConfigurationUI()
    {
        ModLogger.Log("Opening configuration UI");
        ConfigUIRequested?.Invoke();

        if (_bootstrapper?.IsTestEnvironment != true)
        {
            _bootstrapper?.ConfigCoordinator?.OpenConfigurationUI(config => ConfigurationUpdated(config));
        }
    }

    public bool IsConfigUIRequested()
        => ConfigUIRequested != null && ConfigUIRequested.GetInvocationList().Length > 0;

    #endregion

    #region Internal Methods (for testing)

    internal bool IsJobSprite(string fileName)
        => _bootstrapper?.ThemeCoordinator?.IsJobSprite(fileName) ?? false;

    #endregion

    public bool SupportsPerCharacterColors() => true;
}
