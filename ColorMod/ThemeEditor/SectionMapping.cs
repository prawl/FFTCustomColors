using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FFTColorCustomizer.ThemeEditor
{
    public class JobSection
    {
        public string Name { get; }
        public string DisplayName { get; }
        public int[] Indices { get; }
        public string[] Roles { get; }
        public string? LinkedTo { get; }

        public JobSection(string name, string displayName, int[] indices, string[] roles, string? linkedTo = null)
        {
            Name = name;
            DisplayName = displayName;
            Indices = indices;
            Roles = roles;
            LinkedTo = linkedTo;
        }
    }

    public class SectionMapping
    {
        public string Job { get; }
        public string Sprite { get; }
        public JobSection[] Sections { get; }

        public SectionMapping(string job, string sprite, JobSection[] sections)
        {
            Job = job;
            Sprite = sprite;
            Sections = sections;
        }
    }

    public static class SectionMappingLoader
    {
        public static SectionMapping ParseJson(string json)
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var job = root.GetProperty("job").GetString();
            var sprite = root.GetProperty("sprite").GetString();

            var sectionsArray = root.GetProperty("sections");
            var sections = sectionsArray.EnumerateArray().Select(s => new JobSection(
                s.GetProperty("name").GetString(),
                s.GetProperty("displayName").GetString(),
                s.GetProperty("indices").EnumerateArray().Select(i => i.GetInt32()).ToArray(),
                s.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToArray(),
                s.TryGetProperty("linkedTo", out var linkedTo) ? linkedTo.GetString() : null
            )).ToArray();

            return new SectionMapping(job, sprite, sections);
        }

        // Canonical job class order (matching FFT job unlock progression)
        private static readonly string[] JobClassOrder = new[]
        {
            "Squire", "Chemist", "Knight", "Archer", "Monk", "WhiteMage", "BlackMage",
            "TimeMage", "Summoner", "Thief", "Mediator", "Mystic", "Geomancer",
            "Dragoon", "Samurai", "Ninja", "Calculator", "Bard", "Dancer", "Mime"
        };

        public static string[] GetAvailableJobs(string mappingsPath)
        {
            if (!Directory.Exists(mappingsPath))
                return new string[0];

            var jobs = Directory.GetFiles(mappingsPath, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .ToList();

            // Sort by job class order, then gender (Male before Female)
            return jobs
                .OrderBy(job => GetJobClassIndex(job))
                .ThenBy(job => job.EndsWith("_Female") ? 1 : 0)
                .ToArray();
        }

        private static int GetJobClassIndex(string jobName)
        {
            // Extract the job class name (e.g., "Squire" from "Squire_Male")
            var jobClass = jobName.Split('_')[0];
            var index = Array.IndexOf(JobClassOrder, jobClass);
            // Unknown jobs go to the end
            return index >= 0 ? index : int.MaxValue;
        }

        public static SectionMapping LoadFromFile(string filePath)
        {
            var json = File.ReadAllText(filePath);
            return ParseJson(json);
        }
    }
}
