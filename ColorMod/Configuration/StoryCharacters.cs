using ColorMod.Registry;

namespace FFTColorCustomizer.Configuration
{
    [StoryCharacter(SpriteNames = new[] { "cloud" }, DefaultTheme = "sephiroth_black")]
    public enum CloudColorScheme
    {
        sephiroth_black,
        knights_round,
        ultima_weapon,
        buster_sword,
        fusion_sword,
        original
    }

    [StoryCharacter(SpriteNames = new[] { "aguri" }, DefaultTheme = "original")]
    public enum AgriasColorScheme
    {
        original,
        ice_knight,
        holy_knight,
        dark_knight,
        excalibur,
        durandal
    }

    [StoryCharacter(SpriteNames = new[] { "oru", "goru" }, DefaultTheme = "original")]
    public enum OrlandeauColorScheme
    {
        original,
        thunder_god,
        divine_knight,
        chaos_blade,
        excalibur,
        omega_weapon
    }

    [StoryCharacter(SpriteNames = new[] { "kanba" }, DefaultTheme = "original")]
    public enum RamzaColorScheme
    {
        original,
        brave_story,
        crystal_bearer,
        chaos_blade,
        ultima_sword,
        holy_knight
    }
}
