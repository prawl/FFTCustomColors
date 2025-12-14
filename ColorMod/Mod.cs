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
    private DynamicSpriteLoader? _dynamicSpriteLoader;
    private Process? _gameProcess;
    private string _currentColorScheme = "original";
    private ColorSchemeCycler _colorCycler;
    private IInputSimulator? _inputSimulator;
    private bool _configUIRequested = false;
    private string _modPath;
    private string _sourcePath; // Path to git repo for theme sources

    // Constructor that accepts ModContext and optional IInputSimulator (for testing)
    public Mod(ModContext context, IInputSimulator? inputSimulator = null)
    {
        // Do minimal work in constructor - Reloaded will call Start() for initialization
        Console.WriteLine("[FFT Color Mod] Constructor called with ModContext");
        _inputSimulator = inputSimulator;

        // Try to auto-detect sprite variants from mod directory
        _modPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;

        // Set the source path to the git repo location (hardcoded for now, could be made configurable)
        _sourcePath = @"C:\Users\ptyRa\Dev\FFT_Color_Mod\ColorMod";

        // Use source path for detecting themes (from git repo)
        string spritesPath = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit");

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

        // Set source path if not already set (for backward compatibility)
        if (string.IsNullOrEmpty(_sourcePath))
        {
            _sourcePath = @"C:\Users\ptyRa\Dev\FFT_Color_Mod\ColorMod";
        }

        // Initialize sprite file manager with both deployment and source paths
        string modPath = Environment.GetEnvironmentVariable("FFT_MOD_PATH") ??
                        Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ??
                        Environment.CurrentDirectory;
        _spriteFileManager = new SpriteFileManager(modPath, _sourcePath);

        // Initialize configuration-based sprite management
        // IMPORTANT: Use the User directory where Reloaded-II saves configs
        // Path should be: Reloaded/User/Mods/ptyra.fft.colormod/Config.json
        string configPath;
        var envConfigPath = Environment.GetEnvironmentVariable("FFT_CONFIG_PATH");
        Console.WriteLine($"[FFT Color Mod] FFT_CONFIG_PATH env var: '{envConfigPath}'");

        if (!string.IsNullOrEmpty(envConfigPath))
        {
            configPath = envConfigPath;
            Console.WriteLine($"[FFT Color Mod] Using config path from env var: {configPath}");
        }
        else
        {
            // Navigate from Mods/FFT_Color_Mod to User/Mods/ptyra.fft.colormod
            var reloadedRoot = Directory.GetParent(Directory.GetParent(modPath).FullName)?.FullName ?? modPath;
            configPath = Path.Combine(reloadedRoot, "User", "Mods", "ptyra.fft.colormod", "Config.json");

            // Fallback if User config doesn't exist
            if (!File.Exists(configPath))
            {
                Console.WriteLine($"[FFT Color Mod] User config not found at: {configPath}");
                configPath = Path.Combine(modPath, "Config.json");
            }
        }

        Console.WriteLine($"[FFT Color Mod] Final config path: '{configPath}'");
        if (!string.IsNullOrEmpty(configPath))
        {
            Console.WriteLine($"[FFT Color Mod] Creating ConfigurationManager with path: {configPath}");
            _configurationManager = new ConfigurationManager(configPath);
            Console.WriteLine($"[FFT Color Mod] Creating ConfigBasedSpriteManager...");
            _configBasedSpriteManager = new ConfigBasedSpriteManager(modPath, _configurationManager, _sourcePath);
            Console.WriteLine($"[FFT Color Mod] Configuration managers created successfully");
        }
        else
        {
            Console.WriteLine("[FFT Color Mod] WARNING: Config path is null or empty, config-based management disabled");
        }

        // Initialize dynamic sprite loader
        _dynamicSpriteLoader = new DynamicSpriteLoader(modPath, _configurationManager);

        // Load saved configuration and prepare sprites
        var loadedConfig = _configurationManager.LoadConfig();
        Console.WriteLine($"[FFT Color Mod] Loaded config - Knight_Male: {loadedConfig.Knight_Male}");
        Console.WriteLine($"[FFT Color Mod] Current global color scheme: {_currentColorScheme}");

        // Log all configured job colors
        var properties = typeof(Config).GetProperties()
            .Where(p => p.PropertyType == typeof(Configuration.ColorScheme));
        foreach (var prop in properties)
        {
            var value = prop.GetValue(loadedConfig);
            if (value != null && value.ToString() != "original")
            {
                Console.WriteLine($"[FFT Color Mod] Config: {prop.Name} = {value}");
            }
        }

        // Prepare sprites based on configuration (copy from ColorSchemes to data)
        Console.WriteLine("[FFT Color Mod] Preparing sprites based on configuration...");
        _dynamicSpriteLoader.PrepareSpritesForConfig();

        _configBasedSpriteManager.ApplyConfiguration();

        // Apply initial story character themes
        ApplyInitialOrlandeauTheme();
        ApplyInitialBeowulfTheme();
        ApplyInitialAgriasTheme();

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
        Console.WriteLine($"[FFT Color Mod] Color scheme set to: {scheme}");

        // Actually switch the sprite files to apply the color
        _spriteFileManager?.SwitchColorScheme(scheme);

        // Update cycler ONLY if it's out of sync (for direct calls like tests/initialization)
        // The cycler updates itself during GetNext/GetPrevious, but not for direct SetColorScheme calls
        if (_colorCycler?.GetCurrentScheme() != scheme)
        {
            _colorCycler?.SetCurrentScheme(scheme);
        }
    }


    private StoryCharacterThemeManager _storyCharacterManager = new StoryCharacterThemeManager();

    private void ApplyInitialOrlandeauTheme()
    {
        try
        {
            var currentTheme = _storyCharacterManager.GetCurrentOrlandeauTheme();
            Console.WriteLine($"[FFT Color Mod] Applying initial Orlandeau theme: {currentTheme}");

            // Apply the initial theme by copying the sprite file
            string orlandeauThemeDir = $"sprites_orlandeau_{currentTheme.ToString().ToLower()}";
            var sourceFile = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", orlandeauThemeDir, "battle_oru_spr.bin");
            var destFile = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_oru_spr.bin");

            Console.WriteLine($"[FFT Color Mod] Looking for initial Orlandeau sprite at: {sourceFile}");
            if (File.Exists(sourceFile))
            {
                File.Copy(sourceFile, destFile, true);
                Console.WriteLine($"[FFT Color Mod] Successfully applied initial Orlandeau theme: {currentTheme}");

                // Also copy the other Orlandeau variants
                string[] variants = { "battle_goru_spr.bin", "battle_voru_spr.bin" };
                foreach (var variant in variants)
                {
                    var variantSource = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", orlandeauThemeDir, variant);
                    var variantDest = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", variant);
                    if (File.Exists(variantSource))
                    {
                        File.Copy(variantSource, variantDest, true);
                        Console.WriteLine($"[FFT Color Mod] Applied initial theme to {variant}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"[FFT Color Mod] Warning: Initial Orlandeau theme file not found at: {sourceFile}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FFT Color Mod] Error applying initial Orlandeau theme: {ex.Message}");
        }
    }

    private void ApplyInitialBeowulfTheme()
    {
        try
        {
            var currentTheme = _storyCharacterManager.GetCurrentBeowulfTheme();
            Console.WriteLine($"[FFT Color Mod] Applying initial Beowulf theme: {currentTheme}");

            // Apply the initial theme by copying the sprite file
            string beowulfThemeDir = $"sprites_beowulf_{currentTheme.ToString().ToLower()}";
            var sourceFile = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", beowulfThemeDir, "battle_beio_spr.bin");
            var destFile = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_beio_spr.bin");

            Console.WriteLine($"[FFT Color Mod] Looking for initial Beowulf sprite at: {sourceFile}");
            if (File.Exists(sourceFile))
            {
                File.Copy(sourceFile, destFile, true);
                Console.WriteLine($"[FFT Color Mod] Successfully applied initial Beowulf theme: {currentTheme}");
            }
            else
            {
                Console.WriteLine($"[FFT Color Mod] Warning: Initial Beowulf theme file not found at: {sourceFile}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FFT Color Mod] Error applying initial Beowulf theme: {ex.Message}");
        }
    }

    private void ApplyInitialAgriasTheme()
    {
        try
        {
            var currentTheme = _storyCharacterManager.GetCurrentAgriasTheme();
            Console.WriteLine($"[FFT Color Mod] Applying initial Agrias theme: {currentTheme}");

            // Apply the initial theme by copying both Agrias sprite files
            string agriasThemeDir = $"sprites_agrias_{currentTheme.ToString().ToLower()}";

            // Agrias has two sprite files
            string[] agriasSprites = { "battle_aguri_spr.bin", "battle_kanba_spr.bin" };

            foreach (var sprite in agriasSprites)
            {
                var sourceFile = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", agriasThemeDir, sprite);
                var destFile = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", sprite);

                Console.WriteLine($"[FFT Color Mod] Looking for initial Agrias sprite at: {sourceFile}");
                if (File.Exists(sourceFile))
                {
                    File.Copy(sourceFile, destFile, true);
                    Console.WriteLine($"[FFT Color Mod] Successfully applied initial Agrias theme to {sprite}: {currentTheme}");
                }
                else
                {
                    Console.WriteLine($"[FFT Color Mod] Warning: Initial Agrias theme file not found at: {sourceFile}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FFT Color Mod] Error applying initial Agrias theme: {ex.Message}");
        }
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
            // Cycle forward through color schemes
            string nextColor = _colorCycler.GetNextScheme();
            Console.WriteLine("================================================");
            Console.WriteLine("================================================");
            Console.WriteLine($"    GENERIC THEME CHANGED TO: {nextColor}");
            Console.WriteLine("================================================");
            Console.WriteLine("================================================");
            Console.WriteLine($"[FFT Color Mod] Cycling generic forward to {nextColor}");
            SetColorScheme(nextColor);

            // Also cycle Orlandeau theme
            var nextOrlandeauTheme = _storyCharacterManager.CycleOrlandeauTheme();
            Console.WriteLine($"[FFT Color Mod] Orlandeau theme: {nextOrlandeauTheme}");

            // Apply Orlandeau theme by copying the sprite file FROM GIT REPO to deployment
            string orlandeauThemeDir = $"sprites_orlandeau_{nextOrlandeauTheme.ToString().ToLower()}";
            var sourceFile = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", orlandeauThemeDir, "battle_oru_spr.bin");
            var destFile = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_oru_spr.bin");

            Console.WriteLine($"[FFT Color Mod] Looking for Orlandeau sprite at: {sourceFile}");
            if (File.Exists(sourceFile))
            {
                File.Copy(sourceFile, destFile, true);
                Console.WriteLine($"[FFT Color Mod] Successfully copied Orlandeau theme: {nextOrlandeauTheme}");

                // Also copy the other Orlandeau variants
                string[] variants = { "battle_goru_spr.bin", "battle_voru_spr.bin" };
                foreach (var variant in variants)
                {
                    var variantSource = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", orlandeauThemeDir, variant);
                    var variantDest = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", variant);
                    if (File.Exists(variantSource))
                    {
                        File.Copy(variantSource, variantDest, true);
                        Console.WriteLine($"[FFT Color Mod] Applied theme to {variant}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"[FFT Color Mod] ERROR: Orlandeau sprite not found at: {sourceFile}");
            }

            // Also cycle Beowulf theme
            var nextBeowulfTheme = _storyCharacterManager.CycleBeowulfTheme();
            Console.WriteLine($"[FFT Color Mod] Cycling Beowulf to {nextBeowulfTheme}");

            // Apply Beowulf theme by copying the sprite file
            string beowulfThemeDir = $"sprites_beowulf_{nextBeowulfTheme.ToString().ToLower()}";
            var beowulfSourceFile = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", beowulfThemeDir, "battle_beio_spr.bin");
            var beowulfDestFile = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", "battle_beio_spr.bin");

            Console.WriteLine($"[FFT Color Mod] Looking for Beowulf sprite at: {beowulfSourceFile}");
            if (File.Exists(beowulfSourceFile))
            {
                File.Copy(beowulfSourceFile, beowulfDestFile, true);
                Console.WriteLine($"[FFT Color Mod] Successfully copied Beowulf theme: {nextBeowulfTheme}");
            }
            else
            {
                Console.WriteLine($"[FFT Color Mod] ERROR: Beowulf sprite not found at: {beowulfSourceFile}");
            }

            // Also cycle Agrias theme
            var nextAgriasTheme = _storyCharacterManager.CycleAgriasTheme();
            Console.WriteLine("================================================");
            Console.WriteLine("================================================");
            Console.WriteLine($"    AGRIAS THEME CHANGED TO: {nextAgriasTheme}");
            Console.WriteLine("================================================");
            Console.WriteLine("================================================");
            Console.WriteLine($"[FFT Color Mod] Cycling Agrias to {nextAgriasTheme}");

            // Apply Agrias theme by copying both sprite files
            string agriasThemeDir = $"sprites_agrias_{nextAgriasTheme.ToString().ToLower()}";
            string[] agriasSprites = { "battle_aguri_spr.bin", "battle_kanba_spr.bin" };

            foreach (var sprite in agriasSprites)
            {
                var agriasSourceFile = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "fftpack", "unit", agriasThemeDir, sprite);
                var agriasDestFile = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit", sprite);

                Console.WriteLine($"[FFT Color Mod] Looking for Agrias sprite at: {agriasSourceFile}");
                if (File.Exists(agriasSourceFile))
                {
                    File.Copy(agriasSourceFile, agriasDestFile, true);
                    Console.WriteLine($"[FFT Color Mod] Successfully copied Agrias theme for {sprite}: {nextAgriasTheme}");
                }
                else
                {
                    Console.WriteLine($"[FFT Color Mod] ERROR: Agrias sprite not found at: {agriasSourceFile}");
                }
            }

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
            Console.WriteLine("[FFT Color Mod] Opening configuration UI (F3)");
            OpenConfigurationUI();
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

        // F1/F2 global scheme takes priority over per-character config
        // When global scheme is not "original", ALL sprites use it
        if (_currentColorScheme != "original" && _spriteFileManager != null)
        {
            return _spriteFileManager.InterceptFilePath(originalPath, _currentColorScheme);
        }

        // Only use config-based system when global scheme is "original"
        if (_configBasedSpriteManager != null && IsJobSprite(fileName))
        {
            Console.WriteLine($"[FFT Color Mod] Using config-based system for: {fileName}");
            var result = _configBasedSpriteManager.InterceptFilePath(originalPath);
            Console.WriteLine($"[FFT Color Mod] Config result: {originalPath} -> {result}");
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
            "battle_beio_",   // Beowulf
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
        Console.WriteLine($"[FFT Color Mod] SetJobColor called: {jobProperty} = {colorScheme}");
        Console.WriteLine($"[FFT Color Mod] _configBasedSpriteManager is null? {_configBasedSpriteManager == null}");

        if (_configBasedSpriteManager != null)
        {
            _configBasedSpriteManager.SetColorForJob(jobProperty, colorScheme);
        }
        else
        {
            Console.WriteLine("[FFT Color Mod] WARNING: _configBasedSpriteManager is null, cannot set job color");
        }
    }

    public string GetJobColor(string jobProperty)
    {
        return _configBasedSpriteManager?.GetActiveColorForJob(jobProperty) ?? "original";
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
        return _configUIRequested;
    }

    private void OpenConfigurationUI()
    {
        try
        {
            Console.WriteLine("[FFT Color Mod] Opening configuration UI...");

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
                    Console.WriteLine("[FFT Color Mod] Saving configuration...");
                    _configurationManager.SaveConfig(config);

                    // Update the config-based sprite manager with new configuration
                    if (_configBasedSpriteManager != null)
                    {
                        Console.WriteLine("[FFT Color Mod] Updating sprite manager with new configuration...");
                        _configBasedSpriteManager.UpdateConfiguration(config);
                    }

                    // Simulate a menu refresh to reload sprites in-game
                    Console.WriteLine("[FFT Color Mod] Triggering sprite refresh in game...");
                    if (_inputSimulator != null)
                    {
                        Console.WriteLine("[FFT Color Mod] Calling SimulateMenuRefresh...");
                        bool refreshResult = _inputSimulator.SimulateMenuRefresh();
                        Console.WriteLine($"[FFT Color Mod] SimulateMenuRefresh returned: {refreshResult}");
                    }
                    else
                    {
                        Console.WriteLine("[FFT Color Mod] WARNING: InputSimulator is null - cannot refresh sprites!");
                    }
                }

                Console.WriteLine("[FFT Color Mod] Configuration window closed");
            }
            else
            {
                Console.WriteLine("[FFT Color Mod] Warning: Configuration manager not initialized");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FFT Color Mod] Error opening configuration UI: {ex.Message}");
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

        Console.WriteLine("[FFT Color Mod] Configuration updated from Reloaded-II UI");
        Console.WriteLine($"[FFT Color Mod] Squire_Male set to: {configuration.Squire_Male}");
    }
} 