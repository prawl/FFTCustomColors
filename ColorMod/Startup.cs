using System;
using System.IO;
using System.Threading;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;
using FFTColorMod.Configuration;

namespace FFTColorMod;

// Disabled - functionality moved to Mod.cs
// This class is no longer used as the main mod entry point
public class Startup_DISABLED
{
    private Mod _mod = null!;
    private Thread? _hotkeyThread;
    private bool _running = true;
    private string _currentScheme = "original";
    private IModLoaderV1 _modLoader = null!;
    private IModConfigV1 _modConfig = null!;
    private Config _configuration = null!;

    public Startup_DISABLED()
    {
        // Initialize core components
    }

    // This is the method that Reloaded-II actually calls!
    public void StartEx(IModLoaderV1 loaderApi, IModConfigV1 modConfig)
    {
        _modLoader = loaderApi;
        _modConfig = modConfig;

        Console.WriteLine("[FFTColorMod] Starting with file swapping for color changes!");

        // Your config file is in Config.json.
        // Need a different name, format or more configurations? Modify the `Configurator`.
        // If you do not want a config, remove Configuration folder and Config class.
        var modDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        // Calculate the User config directory where Reloaded-II actually stores user configs
        var reloadedRoot = Path.GetDirectoryName(Path.GetDirectoryName(modDirectory));
        var userConfigDir = Path.Combine(reloadedRoot ?? "", "User", "Mods", "ptyra.fft.colormod");

        // Load configuration from the User directory, not the mod directory
        var configurator = new Configurator(userConfigDir);
        _configuration = configurator.GetConfiguration<Config>(0);
        _configuration.ConfigurationUpdated += OnConfigurationUpdated;

        Console.WriteLine("[FFTColorMod] Configuration loaded!");

        // Start hotkey monitoring for file swapping
        StartHotkeyMonitoring();

        Console.WriteLine("[FFTColorMod] File swapping initialized! Press F1 to cycle colors!");

        // Create ModContext with services (will expand later)
        var context = new ModContext();

        // Create our mod instance with context
        _mod = new Mod(context);

        // Apply initial configuration without saving (just notify the mod)
        _mod?.ConfigurationUpdated(_configuration);
    }

    private void OnConfigurationUpdated(Reloaded.Mod.Interfaces.IUpdatableConfigurable config)
    {
        // Calculate the User config directory path
        // From: .../Reloaded/Mods/FFT_Color_Mod/
        // To:   .../Reloaded/User/Mods/ptyra.fft.colormod/Config.json
        var modPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
        var reloadedRoot = Path.GetDirectoryName(Path.GetDirectoryName(modPath));
        var userConfigDir = Path.Combine(reloadedRoot ?? "", "User", "Mods", "ptyra.fft.colormod");
        var userConfigPath = Path.Combine(userConfigDir, "Config.json");

        try
        {
            Directory.CreateDirectory(userConfigDir);

            // Use ConfigurationUpdater to merge and save
            var updater = new Configuration.ConfigurationUpdater();
            updater.UpdateAndSaveConfiguration((Config)config, userConfigPath);

            // Update our internal reference to the merged config
            var savedJson = File.ReadAllText(userConfigPath);
            _configuration = System.Text.Json.JsonSerializer.Deserialize<Config>(savedJson,
                Configuration.Configurable<Config>.SerializerOptions) ?? (Config)config;

            Console.WriteLine($"[FFTColorMod] Configuration saved to USER directory: {userConfigPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FFTColorMod] Failed to save configuration: {ex.Message}");
            _configuration = (Config)config; // Fallback to incoming config on error
        }

        // Then notify the mod
        _mod?.ConfigurationUpdated(_configuration);

        Console.WriteLine("[FFTColorMod] Configuration updated and saved!");
    }

    private void StartHotkeyMonitoring()
    {
        _hotkeyThread = new Thread(() =>
        {
            Console.WriteLine("[FFTColorMod] Hotkey monitoring started - F1 to cycle colors");

            while (_running)
            {
                // F1 hotkey handling disabled - handled by Mod.cs instead
                // The main mod handles F1 to open the configuration UI

                Thread.Sleep(50);
            }
        })
        {
            IsBackground = true,
            Name = "FFTColorMod Hotkey Monitor"
        };

        _hotkeyThread.Start();
    }

    // Import Windows API for key state checking
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    // Required by IMod interface - minimal implementation
    public void Suspend() { }
    public void Resume() { }
    public void Unload() { }
    public bool CanUnload() => false;
    public bool CanSuspend() => false;
    public Action Disposing { get; } = () => { };
}