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
            var original = (FFTColorMod.Configuration.ColorScheme)0;

            // The ToString() returns the enum name
            Assert.Equal("original", original.ToString());

            // Note: It seems the enum names are actually capitalized in the compiled assembly
            // even though they appear lowercase in source
            var enumName = Enum.GetName(typeof(FFTColorMod.Configuration.ColorScheme), original);
            Assert.NotNull(enumName);

            // Parse using the actual enum name returned
            var parsed = Enum.Parse<FFTColorMod.Configuration.ColorScheme>(enumName);
            Assert.Equal(original, parsed);
        }

        [Fact]
        public void CheckEnumValues()
        {
            // Get all enum values
            var values = Enum.GetValues<FFTColorMod.Configuration.ColorScheme>();
            Assert.NotEmpty(values);

            // Check first value
            var first = values[0];
            Assert.Equal("original", first.ToString());

            // Check we can access the enum values directly
            var original = (FFTColorMod.Configuration.ColorScheme)0;
            var corpse = (FFTColorMod.Configuration.ColorScheme)1;
            Assert.Equal(0, (int)original);
            Assert.Equal(1, (int)corpse);
        }
    }
}