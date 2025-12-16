using System.ComponentModel;
using ColorMod.Registry;

namespace FFTColorMod.Configuration
{
    [StoryCharacter(SpriteNames = new[] { "h80" }, DefaultTheme = "original")]
    public enum MeliadoulColorScheme
    {
        [Description("Original")]
        original,

        [Description("Void Black")]
        void_black,
    }
}