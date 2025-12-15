using System;

namespace ColorMod.Registry
{
    [AttributeUsage(AttributeTargets.Enum)]
    public class StoryCharacterAttribute : Attribute
    {
        public string[] SpriteNames { get; set; }
        public string DefaultTheme { get; set; }
    }
}