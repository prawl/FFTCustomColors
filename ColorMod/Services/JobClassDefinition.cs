using System.Collections.Generic;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Represents a generic job class definition loaded from JobClasses.json
    /// </summary>
    public class JobClassDefinition
    {
        /// <summary>
        /// Property name used in Config.cs (e.g., "Knight_Male")
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Display name for UI (e.g., "Knight (Male)")
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// Sprite file name (e.g., "battle_knight_m_spr.bin")
        /// </summary>
        public string SpriteName { get; set; } = "";

        /// <summary>
        /// Default theme (usually "original")
        /// </summary>
        public string DefaultTheme { get; set; } = "original";

        /// <summary>
        /// Gender of the job class
        /// </summary>
        public string Gender { get; set; } = "";

        /// <summary>
        /// Job type without gender (e.g., "Knight", "Archer")
        /// </summary>
        public string JobType { get; set; } = "";

        /// <summary>
        /// Available color themes for this job
        /// All generic jobs share the same ColorScheme enum
        /// </summary>
        public List<string> AvailableThemes { get; set; } = new List<string>();

        /// <summary>
        /// Job-specific themes that are only available for this particular job
        /// </summary>
        public List<string> JobSpecificThemes { get; set; } = new List<string>();

        /// <summary>
        /// Gets all themes available for this job (shared + job-specific)
        /// </summary>
        public List<string> GetAllAvailableThemes(List<string> sharedThemes)
        {
            var allThemes = new List<string>();
            if (sharedThemes != null)
                allThemes.AddRange(sharedThemes);
            if (JobSpecificThemes != null)
                allThemes.AddRange(JobSpecificThemes);
            return allThemes;
        }

        /// <summary>
        /// Checks if a theme is available for this job
        /// </summary>
        public bool IsThemeAvailable(string themeName, List<string> sharedThemes)
        {
            if (string.IsNullOrEmpty(themeName))
                return false;

            // Check shared themes
            if (sharedThemes != null && sharedThemes.Contains(themeName))
                return true;

            // Check job-specific themes
            if (JobSpecificThemes != null && JobSpecificThemes.Contains(themeName))
                return true;

            return false;
        }
    }
}
