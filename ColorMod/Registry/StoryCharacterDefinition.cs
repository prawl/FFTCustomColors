using System;

namespace ColorMod.Registry
{
    public class StoryCharacterDefinition
    {
        public string Name { get; set; }
        public Type EnumType { get; set; }
        public string[] SpriteNames { get; set; }
        public string DefaultTheme { get; set; }
    }
}