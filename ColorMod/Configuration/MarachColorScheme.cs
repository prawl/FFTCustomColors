using System.ComponentModel;
using ColorMod.Registry;

namespace FFTColorMod.Configuration
{
    [StoryCharacter(SpriteNames = new[] { "mara" }, DefaultTheme = "original")]
    public enum MarachColorScheme
    {
        [Description("Original")]
        original,
    }
}