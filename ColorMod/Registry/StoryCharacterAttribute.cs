using System;

namespace ColorMod.Registry
{
    /// <summary>Marks a story character enum with its sprite names and default theme.</summary>
    [AttributeUsage(AttributeTargets.Enum)]
    public class StoryCharacterAttribute : Attribute
    {
        public string[] SpriteNames { get; set; }
        public string DefaultTheme { get; set; }
    }
}