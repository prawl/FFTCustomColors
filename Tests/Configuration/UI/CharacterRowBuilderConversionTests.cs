using Xunit;
using FFTColorCustomizer.Configuration.UI;

namespace Tests.Configuration.UI
{
    /// <summary>
    /// Tests for job name conversion methods in CharacterRowBuilder
    /// </summary>
    public class CharacterRowBuilderConversionTests
    {
        [Theory]
        [InlineData("Knight (Male)", "Knight_Male")]
        [InlineData("Knight (Female)", "Knight_Female")]
        [InlineData("Squire (Male)", "Squire_Male")]
        [InlineData("Squire (Female)", "Squire_Female")]
        [InlineData("White Mage (Male)", "WhiteMage_Male")]
        [InlineData("White Mage (Female)", "WhiteMage_Female")]
        [InlineData("Time Mage (Male)", "TimeMage_Male")]
        [InlineData("Black Mage (Female)", "BlackMage_Female")]
        [InlineData("Bard", "Bard")]
        [InlineData("Dancer", "Dancer")]
        public void ConvertJobNameToPropertyFormat_Should_ConvertDisplayNameToPropertyName(string displayName, string expected)
        {
            // Act
            var result = CharacterRowBuilder.ConvertJobNameToPropertyFormat(displayName);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
