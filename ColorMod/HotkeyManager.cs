namespace FFTColorMod;

public class HotkeyManager
{
    public string CurrentScheme { get; private set; } = "original";

    public void ProcessHotkey(int keyCode)
    {
        switch (keyCode)
        {
            case 0x70: // F1 - White/Silver colors
                CurrentScheme = "white_silver";
                break;
            case 0x71: // F2 - Ocean Blue colors
                CurrentScheme = "ocean_blue";
                break;
            case 0x72: // F3 - Deep Purple colors
                CurrentScheme = "deep_purple";
                break;
            case 0x73: // F4 - Original colors
                CurrentScheme = "original";
                break;
        }
    }
}