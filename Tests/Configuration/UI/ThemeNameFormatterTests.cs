using Xunit;
using FluentAssertions;
using FFTColorCustomizer.Configuration.UI;

namespace FFTColorCustomizer.Tests.Configuration.UI
{
    public class ThemeNameFormatterTests
    {
        [Theory]
        [InlineData("original", "Original")]
        [InlineData("corpse_brigade", "Corpse Brigade")]
        [InlineData("lucavi", "Lucavi")]
        [InlineData("northern_sky", "Northern Sky")]
        [InlineData("southern_sky", "Southern Sky")]
        [InlineData("crimson_red", "Crimson Red")]
        [InlineData("royal_purple", "Royal Purple")]
        [InlineData("phoenix_flame", "Phoenix Flame")]
        [InlineData("frost_knight", "Frost Knight")]
        [InlineData("silver_knight", "Silver Knight")]
        [InlineData("emerald_dragon", "Emerald Dragon")]
        [InlineData("rose_gold", "Rose Gold")]
        [InlineData("ocean_depths", "Ocean Depths")]
        [InlineData("golden_templar", "Golden Templar")]
        [InlineData("blood_moon", "Blood Moon")]
        [InlineData("ash_dark", "Ash Dark")]
        [InlineData("knights_round", "Knights Round")]
        [InlineData("sephiroth_black", "Sephiroth Black")]
        public void FormatThemeName_Should_Format_Theme_Names_Correctly(string input, string expected)
        {
            // Act
            var result = ThemeNameFormatter.FormatThemeName(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Original", "original")]
        [InlineData("Corpse Brigade", "corpse_brigade")]
        [InlineData("Lucavi", "lucavi")]
        [InlineData("Northern Sky", "northern_sky")]
        [InlineData("Southern Sky", "southern_sky")]
        [InlineData("Crimson Red", "crimson_red")]
        [InlineData("Royal Purple", "royal_purple")]
        [InlineData("Phoenix Flame", "phoenix_flame")]
        [InlineData("Frost Knight", "frost_knight")]
        [InlineData("Silver Knight", "silver_knight")]
        [InlineData("Emerald Dragon", "emerald_dragon")]
        [InlineData("Rose Gold", "rose_gold")]
        [InlineData("Ocean Depths", "ocean_depths")]
        [InlineData("Golden Templar", "golden_templar")]
        [InlineData("Blood Moon", "blood_moon")]
        [InlineData("Ash Dark", "ash_dark")]
        [InlineData("Knights Round", "knights_round")]
        [InlineData("Sephiroth Black", "sephiroth_black")]
        public void UnformatThemeName_Should_Convert_Back_To_Internal_Format(string input, string expected)
        {
            // Act
            var result = ThemeNameFormatter.UnformatThemeName(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", "")]
        public void FormatThemeName_Should_Handle_Null_And_Empty(string input, string expected)
        {
            // Act
            var result = ThemeNameFormatter.FormatThemeName(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", "")]
        public void UnformatThemeName_Should_Handle_Null_And_Empty(string input, string expected)
        {
            // Act
            var result = ThemeNameFormatter.UnformatThemeName(input);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("original")]
        [InlineData("corpse_brigade")]
        [InlineData("lucavi")]
        [InlineData("northern_sky")]
        [InlineData("crimson_red")]
        [InlineData("royal_purple")]
        [InlineData("emerald_dragon")]
        [InlineData("blood_moon")]
        public void Format_And_Unformat_Should_Be_Reversible(string original)
        {
            // Act
            var formatted = ThemeNameFormatter.FormatThemeName(original);
            var unformatted = ThemeNameFormatter.UnformatThemeName(formatted);

            // Assert
            unformatted.Should().Be(original);
        }
    }
}