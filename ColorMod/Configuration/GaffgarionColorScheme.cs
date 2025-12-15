using System.ComponentModel;
using ColorMod.Registry;

namespace FFTColorMod.Configuration
{
    [StoryCharacter(SpriteNames = new[] { "baruna" }, DefaultTheme = "original")]
    public enum GaffgarionColorScheme
    {
        [Description("Original")]
        original,

        [Description("Blacksteel Red")]
        blacksteel_red
    }
}