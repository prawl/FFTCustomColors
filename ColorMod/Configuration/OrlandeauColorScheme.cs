using System.ComponentModel;
using ColorMod.Registry;

namespace FFTColorMod.Configuration
{
    [StoryCharacter(SpriteNames = new[] { "oru", "goru" }, DefaultTheme = "thunder_god")]
    public enum OrlandeauColorScheme
    {
        [Description("Original")]
        original,

        [Description("Thunder God")]
        thunder_god,
    }
}