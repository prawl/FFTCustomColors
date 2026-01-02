using Xunit;
using FluentAssertions;

namespace Tests.ThemeEditor
{
    /// <summary>
    /// Tests for job name formatting in the Theme Editor template dropdown
    /// </summary>
    public class JobNameFormattingTests
    {
        [Theory]
        [InlineData("Squire_Male", "Squire (Male)")]
        [InlineData("Squire_Female", "Squire (Female)")]
        [InlineData("Knight_Male", "Knight (Male)")]
        [InlineData("Knight_Female", "Knight (Female)")]
        [InlineData("Chemist_Male", "Chemist (Male)")]
        [InlineData("Chemist_Female", "Chemist (Female)")]
        public void JobNameToDisplayName_Should_Format_With_Parentheses(string jobName, string expected)
        {
            // Arrange & Act
            var result = JobNameToDisplayName(jobName);

            // Assert
            result.Should().Be(expected, $"Job name '{jobName}' should be formatted as '{expected}'");
        }

        // This is the method we need to implement/fix in ThemeEditorPanel
        private static string JobNameToDisplayName(string jobName)
        {
            if (string.IsNullOrEmpty(jobName))
                return jobName;

            // Split on underscore
            var parts = jobName.Split('_');

            // If there's no underscore, return as-is
            if (parts.Length == 1)
                return jobName;

            // If there are exactly 2 parts (Job_Gender format)
            if (parts.Length == 2)
            {
                var jobClass = parts[0];
                var gender = parts[1];
                return $"{jobClass} ({gender})";
            }

            // Fallback: just replace underscores with spaces
            return jobName.Replace("_", " ");
        }
    }
}
