using System.Collections.Generic;
using Newtonsoft.Json;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Configuration for all job classes including shared and job-specific themes
    /// </summary>
    public class JobClassConfig
    {
        /// <summary>
        /// Themes that are available for all job classes
        /// </summary>
        [JsonProperty("sharedThemes")]
        public List<string> SharedThemes { get; set; } = new List<string>();

        /// <summary>
        /// For backward compatibility - maps to SharedThemes
        /// </summary>
        [JsonProperty("availableThemes")]
        private List<string> AvailableThemes
        {
            get => SharedThemes;
            set => SharedThemes = value ?? new List<string>();
        }

        /// <summary>
        /// All job class definitions
        /// </summary>
        [JsonProperty("jobClasses")]
        public List<JobClassDefinition> JobClasses { get; set; } = new List<JobClassDefinition>();
    }
}