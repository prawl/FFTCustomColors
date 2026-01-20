using System;
using System.IO;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Interfaces;
using FFTColorCustomizer.Services;
using FFTColorCustomizer.Utilities;
using static FFTColorCustomizer.Core.ColorModConstants;

namespace FFTColorCustomizer.Core.ModComponents
{
    /// <summary>
    /// Bootstraps the mod by initializing all components and wiring dependencies.
    /// Extracted from Mod.cs to separate concerns.
    /// </summary>
    public class ModBootstrapper
    {
        private readonly string _modPath;
        private readonly string _sourcePath;
        private readonly bool _isTestEnvironment;
        private readonly IInputSimulator? _inputSimulator;
        private readonly IHotkeyHandler? _hotkeyHandler;

        // DI Container
        private ServiceContainer? _serviceContainer;

        // Initialized components
        public ModInitializer? Initializer { get; private set; }
        public ConfigurationCoordinator? ConfigCoordinator { get; private set; }
        public ThemeCoordinator? ThemeCoordinator { get; private set; }
        public HotkeyManager? HotkeyManager { get; private set; }

        /// <summary>
        /// Gets the DI service container. Use this to resolve services.
        /// </summary>
        public ServiceContainer? Services => _serviceContainer;

        public string ModPath => _modPath;
        public string SourcePath => _sourcePath;
        public bool IsTestEnvironment => _isTestEnvironment;

        /// <summary>
        /// Creates a bootstrapper for production use (no test doubles)
        /// </summary>
        public static ModBootstrapper CreateForProduction()
        {
            var modPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                ?? Environment.CurrentDirectory;

            return new ModBootstrapper(
                modPath: modPath,
                isTestEnvironment: false,
                inputSimulator: new InputSimulator(),
                hotkeyHandler: null // Will be set during InitializeHotkeys with callback
            );
        }

        /// <summary>
        /// Creates a bootstrapper for testing
        /// </summary>
        public static ModBootstrapper CreateForTesting(
            ModContext context,
            IInputSimulator? inputSimulator = null,
            IHotkeyHandler? hotkeyHandler = null)
        {
            var modPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                ?? Environment.CurrentDirectory;

            return new ModBootstrapper(
                modPath: modPath,
                isTestEnvironment: hotkeyHandler is NullHotkeyHandler,
                inputSimulator: inputSimulator,
                hotkeyHandler: hotkeyHandler
            );
        }

        private ModBootstrapper(
            string modPath,
            bool isTestEnvironment,
            IInputSimulator? inputSimulator,
            IHotkeyHandler? hotkeyHandler)
        {
            _modPath = modPath ?? throw new ArgumentNullException(nameof(modPath));
            _sourcePath = _modPath; // In deployment, source and mod path are the same
            _isTestEnvironment = isTestEnvironment;
            _inputSimulator = inputSimulator;
            _hotkeyHandler = hotkeyHandler;

            ModLogger.Log($"ModBootstrapper initialized with path: {_modPath}");
        }

        /// <summary>
        /// Initialize core components (initializer, theme coordinator)
        /// </summary>
        public void InitializeCoreComponents()
        {
            try
            {
                ModLogger.Log("Initializing core mod components");

                // Initialize DI container and register all services
                _serviceContainer = new ServiceContainer();
                ServiceRegistry.ConfigureServices(_serviceContainer, _modPath);

                // Create initializer and set up core components
                Initializer = new ModInitializer(_modPath, _isTestEnvironment);
                Initializer.InitializeRegistry();
                Initializer.InitializeColorSchemeCycler();

                // Initialize theme coordinator
                ThemeCoordinator = new ThemeCoordinator(_sourcePath, _modPath);

                ModLogger.Log("Core mod components initialized successfully");
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"Failed to initialize core components: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Initialize configuration system
        /// </summary>
        public void InitializeConfiguration(string configPath)
        {
            try
            {
                ModLogger.Log($"Initializing configuration at: {configPath}");

                // Services are already registered in InitializeCoreComponents via ServiceRegistry
                // The ServiceRegistry also syncs with static singletons for backward compatibility

                // Initialize configuration coordinator with injected services
                if (_serviceContainer != null && _serviceContainer.TryResolve<JobClassDefinitionService>(out var jobClassService) && jobClassService != null)
                {
                    ConfigCoordinator = new ConfigurationCoordinator(configPath, _modPath, jobClassService);
                    ModLogger.Log("ConfigurationCoordinator created with DI");
                }
                else
                {
                    // Fallback to singleton-based creation
                    ConfigCoordinator = new ConfigurationCoordinator(configPath, _modPath);
                    ModLogger.Log("ConfigurationCoordinator created with singleton fallback");
                }

                ModLogger.Log("Configuration initialized successfully");
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"Failed to initialize configuration: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Initialize hotkey handling
        /// </summary>
        public void InitializeHotkeys(
            Action<int> hotkeyCallback,
            Action openConfigUIAction,
            Action resetToDefaultsAction)
        {
            var themeManager = ThemeCoordinator?.GetThemeManager();
            if (themeManager == null)
            {
                ModLogger.LogWarning("ThemeManager not available, hotkey manager will have limited functionality");
            }

            // Create hotkey handler if not provided (production mode)
            var handler = _hotkeyHandler ?? new HotkeyHandler(hotkeyCallback);

            HotkeyManager = new HotkeyManager(
                _inputSimulator,
                themeManager,
                openConfigUIAction,
                resetToDefaultsAction
            );

            ModLogger.Log("Hotkey manager initialized");
        }

        /// <summary>
        /// Apply initial configuration themes to story characters
        /// </summary>
        public void ApplyInitialStoryCharacterThemes(Config config)
        {
            if (ThemeCoordinator == null || Initializer == null) return;

            var themeManager = ThemeCoordinator.GetThemeManager();
            if (themeManager != null)
            {
                Initializer.InitializeStoryCharacterThemes(config, themeManager);
            }
        }

        /// <summary>
        /// Get or create hotkey handler
        /// </summary>
        public IHotkeyHandler GetOrCreateHotkeyHandler(Action<int> callback)
        {
            return _hotkeyHandler ?? new HotkeyHandler(callback);
        }

        /// <summary>
        /// Resolves the user config path from the mod path
        /// </summary>
        public string ResolveUserConfigPath()
        {
            return FFTIVCPathResolver.ResolveUserConfigPath(_modPath);
        }

        /// <summary>
        /// Resolves a service from the DI container.
        /// Falls back to static singletons if container is not initialized.
        /// </summary>
        public T GetService<T>() where T : class
        {
            return ServiceRegistry.ResolveWithFallback<T>(_serviceContainer);
        }

        /// <summary>
        /// Tries to resolve a service from the DI container.
        /// </summary>
        public bool TryGetService<T>(out T? service) where T : class
        {
            service = null;
            if (_serviceContainer == null) return false;
            return _serviceContainer.TryResolve(out service);
        }
    }
}
