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
            // Try accessing enum by its integer value
            var original = (ColorScheme)0;

            // The ToString() returns the Description attribute value
            Assert.Equal("Original", original.ToString());

            // Note: It seems the enum names are actually capitalized in the compiled assembly
            // even though they appear lowercase in source
            var enumName = Enum.GetName(typeof(ColorScheme), original);
            Assert.NotNull(enumName);

            // Parse using the actual enum name returned
            var parsed = Enum.Parse<ColorScheme>(enumName);
            Assert.Equal(original, parsed);
        }

        [Fact]
        public void CheckEnumValues()
        {
            // Get all enum values
            var values = Enum.GetValues<ColorScheme>();
            Assert.NotEmpty(values);

            // Check first value
            var first = values[0];
            Assert.Equal("Original", first.ToString());

            // Check we can access the enum values directly
            var original = (ColorScheme)0;
            var corpse = (ColorScheme)1;
            Assert.Equal(0, (int)original);
            Assert.Equal(1, (int)corpse);
        }
    }
}