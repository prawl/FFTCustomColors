using System;
using System.IO;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;

namespace FFTColorMod;

// Minimal implementation to make the test pass
public class Startup : IMod
{
    private Mod _mod = null!;
    public ColorPreferencesManager ColorPreferencesManager { get; private set; } = null!;

    public Startup()
    {
        // TLDR: Initialize ColorPreferencesManager with config file in AppData
        var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FFTColorMod");
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, "preferences.json");
        ColorPreferencesManager = new ColorPreferencesManager(configPath);
    }

    // This is the method that Reloaded-II actually calls!
    public void StartEx(IModLoaderV1 loaderApi, IModConfigV1 modConfig)
    {
        // Create ModContext with services (will expand later)
        var context = new ModContext();

        // Create our mod instance with context
        _mod = new Mod(context);
    }

    // Required by IMod interface - minimal implementation
    public void Suspend() { }
    public void Resume() { }
    public void Unload() { }
    public bool CanUnload() => false;
    public bool CanSuspend() => false;
    public Action Disposing { get; } = () => { };
}