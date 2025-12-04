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
            case 0x72: // F3 - Blue colors
                CurrentScheme = "blue";
                break;
            case 0x73: // F4 - Green colors
                CurrentScheme = "green";
                break;
            case 0x74: // F5 - Purple colors
                CurrentScheme = "purple";
                break;
        }
    }
}