using System.ComponentModel;
using ColorMod.Registry;

namespace FFTColorMod.Configuration
{
    [StoryCharacter(SpriteNames = new[] { "cloud" }, DefaultTheme = "sephiroth_black")]
    public enum CloudColorScheme
    {
        [Description("Original")]
        original,

        [Description("Knights Round")]
        knights_round,

        [Description("Sephiroth Black")]
        sephiroth_black,
    }
}