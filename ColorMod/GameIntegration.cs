using System;

namespace FFTColorMod;

public class GameIntegration
{
    private readonly ColorSchemeCycler _colorCycler;

    public bool IsFileHookActive { get; private set; }
    public Func<string, string?>? FileHookCallback { get; set; }
    public string? LastAppliedScheme { get; private set; }

    public GameIntegration()
    {
        // Initialize components for game integration
        _colorCycler = new ColorSchemeCycler();
        _colorCycler.SetCurrentScheme("original");
        LastAppliedScheme = "original";
    }

    public void ProcessHotkey(int keyCode)
    {
        // Process F1 to cycle colors
        if (keyCode == 0x70) // F1
        {
            string nextScheme = _colorCycler.GetNextScheme();
            _colorCycler.SetCurrentScheme(nextScheme);
            LastAppliedScheme = nextScheme;
        }
    }

    public void InitializeFileHook()
    {
        // TLDR: Initialize file redirection hook for sprite swapping
        IsFileHookActive = true;
    }

    public string GetRedirectedPath(string originalPath)
    {
        // TLDR: Redirect sprite paths based on current color scheme
        if (LastAppliedScheme == "original" || string.IsNullOrEmpty(LastAppliedScheme))
            return originalPath;

        // Only redirect sprite files
        if (!originalPath.Contains("sprites"))
            return originalPath;

        // Replace sprites with color variant folder
        return originalPath.Replace(@"sprites\", $@"sprites_{LastAppliedScheme}\");
    }

    public void RegisterFileHookWithModLoader()
    {
        // TLDR: Register file redirection callback with mod loader
        FileHookCallback = GetRedirectedPath;
        IsFileHookActive = true;
    }

    public string? InvokeFileHookCallback(string originalPath)
    {
        // TLDR: Invoke registered file hook callback
        return FileHookCallback?.Invoke(originalPath);
    }
}