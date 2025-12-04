namespace FFTColorMod;

public class HotkeyManager
{
    public string CurrentScheme { get; private set; } = "original";

    public void ProcessHotkey(int keyCode)
    {
        switch (keyCode)
        {
            case 0x70: // F1 - Original colors
                CurrentScheme = "original";
                break;
            case 0x71: // F2 - Red colors
                CurrentScheme = "red";
                break;
        }
    }
}