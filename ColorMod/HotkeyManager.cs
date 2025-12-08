namespace FFTColorMod;

public class HotkeyManager
{
    public string CurrentScheme { get; set; } = "original";

    public void ProcessHotkey(int keyCode)
    {
        // Hotkey processing is now handled by Mod.ProcessHotkeyPress
        // This class just stores the current scheme for compatibility
    }

    public void SetCurrentColor(string scheme)
    {
        CurrentScheme = scheme;
    }
}