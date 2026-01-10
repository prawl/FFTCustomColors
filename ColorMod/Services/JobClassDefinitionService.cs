using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Service for managing job class definitions loaded from JobClasses.json
    /// </summary>
    public class JobClassDefinitionService
    {
        private readonly Dictionary<string, JobClassDefinition> _jobClasses;
        private readonly Dictionary<string, JobClassDefinition> _jobClassesBySprite;
        private readonly List<string> _availableThemes;
        private readonly List<string> _wotlThemes;
        private readonly HashSet<string> _wotlJobNames;
        private readonly string _dataPath;

        public JobClassDefinitionService(string modPath = null)
        {
            _jobClasses = new Dictionary<string, JobClassDefinition>();
            _jobClassesBySprite = new Dictionary<string, JobClassDefinition>();
            _availableThemes = new List<string>();
            _wotlThemes = new List<string>();
            _wotlJobNames = new HashSet<string>();

            // Determine path to Data directory
            if (string.IsNullOrEmpty(modPath))
            {
                // Fall back to current directory if no mod path provided
                _dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            }
            else
            {
                _dataPath = Path.Combine(modPath, "Data");
            }

            LoadJobClasses();
        }

        /// <summary>
        /// Load job class definitions from JobClasses.json and WotLClasses.json
        /// </summary>
        private void LoadJobClasses()
        {
            // Load main job classes
            LoadJobClassesFromFile("JobClasses.json", isWotL: false);

            // Load WotL job classes (separate file for cleaner organization)
            LoadJobClassesFromFile("WotLClasses.json", isWotL: true);
        }

        /// <summary>
        /// Load job class definitions from a specific JSON file
        /// </summary>
        private void LoadJobClassesFromFile(string fileName, bool isWotL = false)
        {
            try
            {
                var jsonPath = Path.Combine(_dataPath, fileName);
                ModLogger.LogDebug($"Looking for {fileName} at: {jsonPath}");

                if (!File.Exists(jsonPath))
                {
                    ModLogger.LogDebug($"{fileName} not found at: {jsonPath}");
                    return;
                }

                var jsonContent = File.ReadAllText(jsonPath);
                var document = JsonDocument.Parse(jsonContent);
                var root = document.RootElement;

                // Load shared themes from JSON (changed from "availableThemes" to "sharedThemes")
                // Store themes in appropriate list based on whether this is a WotL file
                if (root.TryGetProperty("sharedThemes", out var themesArray))
                {
                    var targetThemeList = isWotL ? _wotlThemes : _availableThemes;
                    foreach (var theme in themesArray.EnumerateArray())
                    {
                        var themeName = theme.GetString();
                        if (!string.IsNullOrEmpty(themeName) && !targetThemeList.Contains(themeName))
                        {
                            targetThemeList.Add(themeName);
                        }
                    }
                    ModLogger.Log($"Loaded {targetThemeList.Count} shared themes from {fileName}: {string.Join(", ", targetThemeList)}");
                }

                if (root.TryGetProperty("jobClasses", out var jobClassesArray))
                {
                    var loadedCount = 0;
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

                        // Load job-specific themes if present
                        jobClass.JobSpecificThemes = new List<string>();
                        if (jobElement.TryGetProperty("jobSpecificThemes", out var jobThemesArray))
                        {
                            foreach (var theme in jobThemesArray.EnumerateArray())
                            {
                                var themeName = theme.GetString();
                                if (!string.IsNullOrEmpty(themeName))
                                {
                                    jobClass.JobSpecificThemes.Add(themeName);
                                }
                            }
                        }

                        // Set available themes based on whether this is a WotL job or regular job
                        var themeSource = isWotL ? _wotlThemes : _availableThemes;
                        jobClass.AvailableThemes = new List<string>(themeSource);

                        _jobClasses[jobClass.Name] = jobClass;

                        // Track WotL job names for lookup
                        if (isWotL)
                        {
                            _wotlJobNames.Add(jobClass.Name);
                        }

                        // Also index by sprite name for quick lookup
                        if (!string.IsNullOrEmpty(jobClass.SpriteName))
                        {
                            _jobClassesBySprite[jobClass.SpriteName] = jobClass;
                        }

                        loadedCount++;
                    }

                    ModLogger.Log($"Loaded {loadedCount} job class definitions from {fileName}");
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"Failed to load {fileName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Get available shared themes for generic job classes
        /// </summary>
        public List<string> GetAvailableThemes()
        {
            // Return shared themes loaded from JobClasses.json
            // If no themes were loaded, return a default set
            if (_availableThemes.Count == 0)
            {
                return new List<string> { "original" };
            }
            return new List<string>(_availableThemes);
        }

        /// <summary>
        /// Get all available themes for a specific job (original first, then job-specific, then other shared)
        /// WotL jobs only get WotL themes, not regular job themes
        /// </summary>
        public List<string> GetAvailableThemesForJob(string jobName)
        {
            var themes = new List<string>();

            // Always add "original" first
            themes.Add("original");

            // Add job-specific themes if the job exists
            if (_jobClasses.TryGetValue(jobName, out var jobClass) && jobClass.JobSpecificThemes != null)
            {
                themes.AddRange(jobClass.JobSpecificThemes);
            }

            // Determine which shared theme list to use based on job type
            var isWotLJob = _wotlJobNames.Contains(jobName);
            var sharedThemes = isWotLJob ? _wotlThemes : _availableThemes;

            // Then add other shared themes (excluding "original" since it's already first)
            foreach (var theme in sharedThemes)
            {
                if (theme != "original")
                {
                    themes.Add(theme);
                }
            }

            return themes;
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
