using System;
using System.Globalization;

namespace FFTColorCustomizer.Configuration.UI
{
    /// <summary>
    /// Utility class for formatting theme names for display in the UI
    /// </summary>
    public static class ThemeNameFormatter
    {
        /// <summary>
        /// Formats theme name for display by removing underscores and capitalizing words
        /// Example: "corpse_brigade" becomes "Corpse Brigade"
        /// </summary>
        public static string FormatThemeName(string themeName)
        {
            if (string.IsNullOrEmpty(themeName))
                return themeName;

            // Replace underscores with spaces
            var words = themeName.Replace('_', ' ').Split(' ');
            var textInfo = CultureInfo.CurrentCulture.TextInfo;

            // Capitalize each word
            for (int i = 0; i < words.Length; i++)
            {
                if (!string.IsNullOrEmpty(words[i]))
                {
                    words[i] = textInfo.ToTitleCase(words[i].ToLower());
                }
            }

            return string.Join(" ", words);
        }

        /// <summary>
        /// Converts display theme name back to internal format
        /// Example: "Corpse Brigade" becomes "corpse_brigade"
        /// </summary>
        public static string UnformatThemeName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
                return displayName;

            // Convert back to snake_case
            return displayName.ToLower().Replace(' ', '_');
        }
    }
}