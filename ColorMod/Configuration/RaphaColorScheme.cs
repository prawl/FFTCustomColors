using System.ComponentModel;
using ColorMod.Registry;

namespace FFTColorMod.Configuration
{
    [StoryCharacter(SpriteNames = new[] { "h79" }, DefaultTheme = "original")]
    public enum RaphaColorScheme
    {
        [Description("Original")]
        original,

        [Description("Twilight Blend")]
        twilight_blend,
    }
}