using System.ComponentModel;
using ColorMod.Registry;

namespace FFTColorMod.Configuration
{
    [StoryCharacter(SpriteNames = new[] { "aguri", "kanba" }, DefaultTheme = "original")]
    public enum AgriasColorScheme
    {
        [Description("Original")]
        original,

        [Description("Ash Dark")]
        ash_dark,
    }
}