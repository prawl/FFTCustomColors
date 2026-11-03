using System;

namespace ColorMod.Registry
{
    /// <summary>One story character's name, enum type, sprite names and default theme.</summary>
    public class StoryCharacterDefinition
    {
        public string Name { get; set; }
        public Type EnumType { get; set; }
        public string[] SpriteNames { get; set; }
        public string DefaultTheme { get; set; }
    }
}