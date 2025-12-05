namespace FFTColorMod;

public class HotkeyManager
{
    public string CurrentScheme { get; private set; } = "original";

    public void ProcessHotkey(int keyCode)
    {
        switch (keyCode)
        {
            case 0x31: // 1 key - Red colors (awesome color #1)
                CurrentScheme = "red";
                break;
            case 0x32: // 2 key - Blue colors (awesome color #2)
                CurrentScheme = "blue";
                break;
            case 0x33: // 3 key - Green colors (awesome color #3)
                CurrentScheme = "green";
                break;
            case 0x34: // 4 key - Original colors
                CurrentScheme = "original";
                break;
        }
    }
}