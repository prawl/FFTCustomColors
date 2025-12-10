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
    }
}