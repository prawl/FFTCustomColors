using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FFTColorMod.Utilities;

namespace FFTColorMod.Services
{
    /// <summary>
    /// Service for managing job class definitions loaded from JobClasses.json
    /// </summary>
    public class JobClassDefinitionService
    {
        private readonly Dictionary<string, JobClassDefinition> _jobClasses;
        private readonly Dictionary<string, JobClassDefinition> _jobClassesBySprite;
        private readonly string _dataPath;

        public JobClassDefinitionService(string modPath = null)
        {
            _jobClasses = new Dictionary<string, JobClassDefinition>();
            _jobClassesBySprite = new Dictionary<string, JobClassDefinition>();

            // Determine path to Data directory
            if (string.IsNullOrEmpty(modPath))
            {
                // Use source path during development
                _dataPath = Path.Combine(@"C:\Users\ptyRa\Dev\FFT_Color_Mod\ColorMod", "Data");
            }
            else
            {
                _dataPath = Path.Combine(modPath, "Data");
            }

            LoadJobClasses();
        }

        /// <summary>
        /// Load job class definitions from JobClasses.json
        /// </summary>
        private void LoadJobClasses()
        {
            try
            {
                var jsonPath = Path.Combine(_dataPath, "JobClasses.json");

                if (!File.Exists(jsonPath))
                {
                    ModLogger.LogWarning($"JobClasses.json not found at: {jsonPath}");
                    return;
                }

                var jsonContent = File.ReadAllText(jsonPath);
                var document = JsonDocument.Parse(jsonContent);
                var root = document.RootElement;

                if (root.TryGetProperty("jobClasses", out var jobClassesArray))
                {
                    foreach (var jobElement in jobClassesArray.EnumerateArray())
                    {
                        var jobClass = new JobClassDefinition
                        {
                            Name = jobElement.GetProperty("name").GetString() ?? "",
                            DisplayName = jobElement.GetProperty("displayName").GetString() ?? "",
                            SpriteName = jobElement.GetProperty("spriteName").GetString() ?? "",
                            DefaultTheme = jobElement.GetProperty("defaultTheme").GetString() ?? "original",
                            Gender = jobElement.GetProperty("gender").GetString() ?? "",
                            JobType = jobElement.GetProperty("jobType").GetString() ?? ""
                        };

                        // Add available themes (all generic jobs use ColorScheme enum)
                        jobClass.AvailableThemes = GetAvailableThemes();

                        _jobClasses[jobClass.Name] = jobClass;

                        // Also index by sprite name for quick lookup
                        if (!string.IsNullOrEmpty(jobClass.SpriteName))
                        {
                            _jobClassesBySprite[jobClass.SpriteName] = jobClass;
                        }
                    }

                    ModLogger.Log($"Loaded {_jobClasses.Count} job class definitions from JobClasses.json");
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"Failed to load JobClasses.json: {ex.Message}");
            }
        }

        /// <summary>
        /// Get available themes for generic job classes
        /// </summary>
        private List<string> GetAvailableThemes()
        {
            // All generic jobs use the same ColorScheme enum
            return new List<string>
            {
                "original",
                "corpse_brigade",
                "lucavi",
                "northern_sky",
                "southern_sky",
                "crimson_red",
                "royal_purple",
                "phoenix_flame",
                "frost_knight",
                "silver_knight",
                "emerald_dragon",
                "rose_gold",
                "ocean_depths",
                "golden_templar",
                "blood_moon",
                "celestial",
                "volcanic",
                "amethyst"
            };
        }

        /// <summary>
        /// Get all job class definitions
        /// </summary>
        public IEnumerable<JobClassDefinition> GetAllJobClasses()
        {
            return _jobClasses.Values;
        }

        /// <summary>
        /// Get a job class by name
        /// </summary>
        public JobClassDefinition GetJobClassByName(string name)
        {
            return _jobClasses.TryGetValue(name, out var jobClass) ? jobClass : null;
        }

        /// <summary>
        /// Get a job class by sprite name
        /// </summary>
        public JobClassDefinition GetJobClassBySpriteName(string spriteName)
        {
            return _jobClassesBySprite.TryGetValue(spriteName, out var jobClass) ? jobClass : null;
        }

        /// <summary>
        /// Get all job classes of a specific type
        /// </summary>
        public IEnumerable<JobClassDefinition> GetJobClassesByType(string jobType)
        {
            return _jobClasses.Values.Where(j => j.JobType.Equals(jobType, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get all job classes by gender
        /// </summary>
        public IEnumerable<JobClassDefinition> GetJobClassesByGender(string gender)
        {
            return _jobClasses.Values.Where(j => j.Gender.Equals(gender, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Check if a sprite name belongs to a generic job
        /// </summary>
        public bool IsGenericJobSprite(string spriteName)
        {
            return _jobClassesBySprite.ContainsKey(spriteName);
        }

        /// <summary>
        /// Add or update a job class definition
        /// </summary>
        public void AddOrUpdateJobClass(JobClassDefinition jobClass)
        {
            if (jobClass == null || string.IsNullOrEmpty(jobClass.Name))
                return;

            _jobClasses[jobClass.Name] = jobClass;

            if (!string.IsNullOrEmpty(jobClass.SpriteName))
            {
                _jobClassesBySprite[jobClass.SpriteName] = jobClass;
            }
        }

        /// <summary>
        /// Remove a job class definition
        /// </summary>
        public bool RemoveJobClass(string name)
        {
            if (_jobClasses.TryGetValue(name, out var jobClass))
            {
                _jobClasses.Remove(name);

                if (!string.IsNullOrEmpty(jobClass.SpriteName))
                {
                    _jobClassesBySprite.Remove(jobClass.SpriteName);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Save job classes back to JSON
        /// </summary>
        public void SaveJobClasses()
        {
            try
            {
                var jsonPath = Path.Combine(_dataPath, "JobClasses.json");

                var jobClassesData = new
                {
                    jobClasses = _jobClasses.Values.Select(j => new
                    {
                        name = j.Name,
                        displayName = j.DisplayName,
                        spriteName = j.SpriteName,
                        defaultTheme = j.DefaultTheme,
                        gender = j.Gender,
                        jobType = j.JobType
                    }).ToArray()
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(jobClassesData, options);
                File.WriteAllText(jsonPath, json);

                ModLogger.Log($"Saved {_jobClasses.Count} job classes to JobClasses.json");
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"Failed to save JobClasses.json: {ex.Message}");
            }
        }
    }
}