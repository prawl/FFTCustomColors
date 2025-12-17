using System.Collections.Generic;
using FFTColorMod.Configuration;

namespace FFTColorMod.Interfaces
{
    /// <summary>
    /// Service for managing themes and their application
    /// </summary>
    public interface IThemeService
    {
        /// <summary>
        /// Applies a theme to a character
        /// </summary>
        void ApplyTheme(string characterName, string themeName);

        /// <summary>
        /// Cycles to the next available theme for a character
        /// </summary>
        string CycleTheme(string characterName);

        /// <summary>
        /// Gets all available themes for a character
        /// </summary>
        IEnumerable<string> GetAvailableThemes(string characterName);

        /// <summary>
        /// Gets the current theme for a character
        /// </summary>
        string GetCurrentTheme(string characterName);

        /// <summary>
        /// Applies themes based on configuration
        /// </summary>
        void ApplyConfigurationThemes(Config config);
    }
}