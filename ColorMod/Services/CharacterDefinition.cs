using System;

namespace FFTColorCustomizer.Services
{
    public class CharacterDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string[] SpriteNames { get; set; } = Array.Empty<string>();
        public string DefaultTheme { get; set; } = "original";
        // "Story" characters and "NPC" characters render in separate config-window sections
        public string Category { get; set; } = "Story";
        public string[] AvailableThemes { get; set; } = Array.Empty<string>();
        public string? EnumType { get; set; }
    }
}
