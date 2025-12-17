using System;

namespace FFTColorCustomizer.Services
{
    public class CharacterDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string[] SpriteNames { get; set; } = Array.Empty<string>();
        public string DefaultTheme { get; set; } = "original";
        public string[] AvailableThemes { get; set; } = Array.Empty<string>();
        public string? EnumType { get; set; }
    }
}
