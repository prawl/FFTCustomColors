using System;
using System.ComponentModel;
using System.Reflection;

namespace FFTColorMod.Configuration
{
    public static class ColorSchemeExtensions
    {
        public static string GetDescription(this ColorScheme colorScheme)
        {
            var fieldInfo = colorScheme.GetType().GetField(colorScheme.ToString());
            if (fieldInfo == null) return colorScheme.ToString();

            var attribute = fieldInfo.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? colorScheme.ToString();
        }

        public static string GetDescription(this AgriasColorScheme colorScheme)
        {
            if (colorScheme == AgriasColorScheme.original)
                return "sprites_original";
            return $"sprites_agrias_{colorScheme.ToString().ToLower()}";
        }

        public static string GetDescription(this OrlandeauColorScheme colorScheme)
        {
            if (colorScheme == OrlandeauColorScheme.original)
                return "sprites_original";
            return $"sprites_orlandeau_{colorScheme.ToString().ToLower()}";
        }

        public static string GetDescription(this BeowulfColorScheme colorScheme)
        {
            if (colorScheme == BeowulfColorScheme.original)
                return "sprites_original";
            return $"sprites_beowulf_{colorScheme.ToString().ToLower()}";
        }
    }
}