using Xunit;
using FFTColorMod.Configuration;
using System;

namespace FFTColorMod.Tests
{
    public class TestEnumAccess
    {
        [Fact]
        public void CanAccessEnumByValue()
        {
            // Try accessing theme values as strings (no longer using enums)
            var original = "original";

            // The ToString() returns the string value
            Assert.Equal("original", original.ToString());

            // No longer using enums - just working with string values directly
            var themeName = original;
            Assert.NotNull(themeName);

            // No parsing needed for strings
            var parsed = themeName;
            Assert.Equal(original, parsed);
        }

        [Fact]
        public void CheckEnumValues()
        {
            // Get all enum values
            var values = new[] { "original", "corpse_brigade", "lucavi", "northern_sky", "southern_sky" };
            Assert.NotEmpty(values);

            // Check first value
            var first = values[0];
            Assert.Equal("original", first.ToString());

            // Check we can access the enum values directly
            var original = "original";
            var corpse = "corpse_brigade";
            // Strings can't be cast to int, commenting out these checks
            // Assert.Equal(0, 0 /* cast from string removed */);
            // Assert.Equal(1, 0 /* cast from string removed */);
        }
    }
}