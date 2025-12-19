using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Services;
using FluentAssertions;
using Xunit;

namespace Tests.Configuration.UI
{
    public class JobSpecificThemeLoaderTests : IDisposable
    {
        private readonly string _testModPath;
        private readonly JobClassDefinitionService _jobClassService;

        public JobSpecificThemeLoaderTests()
        {
            _testModPath = Path.Combine(Path.GetTempPath(), $"FFTColorTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testModPath);

            // Load the actual JobClasses.json to get the real job-specific themes
            _jobClassService = new JobClassDefinitionService();
        }

        [Fact]
        public void GetThemeFolderPath_WithJobSpecificTheme_ShouldReturnCorrectPath()
        {
            // Arrange
            var jobName = "Knight (Male)";
            var theme = "holy_guard";

            // Act
            var folderPath = GetThemeFolderPath(jobName, theme);

            // Assert
            folderPath.Should().Be("sprites_knight_holy_guard",
                "job-specific themes should use pattern sprites_{jobtype}_{theme}");
        }

        [Fact]
        public void GetThemeFolderPath_WithGenericTheme_ShouldReturnGenericPath()
        {
            // Arrange
            var jobName = "Knight (Male)";
            var theme = "crimson_red";

            // Act
            var folderPath = GetThemeFolderPath(jobName, theme);

            // Assert
            folderPath.Should().Be("sprites_crimson_red",
                "generic themes should use pattern sprites_{theme}");
        }

        [Fact]
        public void IsJobSpecificTheme_ShouldIdentifyJobSpecificThemes()
        {
            // Arrange
            var jobSpecificThemes = new[] { "holy_guard", "divine_blade", "dark_knight", "thunder_general", "summoner_sage" };
            var genericThemes = new[] { "crimson_red", "lucavi", "northern_sky" };

            // Act & Assert - job-specific themes
            foreach (var theme in jobSpecificThemes)
            {
                IsJobSpecificTheme("Knight_Male", theme).Should().BeTrue(
                    $"{theme} should be recognized as a job-specific theme for Knight_Male");
            }

            // Act & Assert - generic themes
            foreach (var theme in genericThemes)
            {
                IsJobSpecificTheme("Knight_Male", theme).Should().BeFalse(
                    $"{theme} should NOT be recognized as a job-specific theme");
            }
        }

        // This method mimics what CharacterRowBuilder does
        private string GetThemeFolderPath(string jobName, string theme)
        {
            // Check if this is a job-specific theme
            string jobType = jobName.Replace(" (Male)", "").Replace(" (Female)", "").Replace(" ", "").ToLower();

            // Check if the theme is job-specific by looking at available themes
            if (IsJobSpecificTheme(jobName.Replace(" (Male)", "_Male").Replace(" (Female)", "_Female"), theme))
            {
                return $"sprites_{jobType}_{theme.ToLower().Replace(" ", "_")}";
            }

            // Otherwise it's a generic theme
            return $"sprites_{theme.ToLower().Replace(" ", "_")}";
        }

        private bool IsJobSpecificTheme(string jobName, string theme)
        {
            // Get the job definition from the service
            var jobDef = _jobClassService.GetJobClassByName(jobName);

            // If we have a job definition, check if the theme is in its job-specific themes list
            if (jobDef != null && jobDef.JobSpecificThemes != null)
            {
                return jobDef.JobSpecificThemes.Contains(theme);
            }

            return false;
        }

        public void Dispose()
        {
            if (Directory.Exists(_testModPath))
            {
                try { Directory.Delete(_testModPath, true); }
                catch { /* Ignore */ }
            }
        }
    }
}