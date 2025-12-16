using System;
using Xunit;

namespace FFTColorMod.Tests
{
    public class SimplePropertyTest
    {
        [Fact]
        public void TestPropertyLookup()
        {
            var configType = typeof(FFTColorMod.Configuration.Config);

            // Test exact property names
            var archerFemaleUnderscore = configType.GetProperty("Archer_Female");
            Assert.NotNull(archerFemaleUnderscore);
            Assert.Equal("Archer_Female", archerFemaleUnderscore.Name);

            // Verify it's the right type
            Assert.Equal(typeof(string), archerFemaleUnderscore.PropertyType);

            // Create a config and test setting the value
            var config = new FFTColorMod.Configuration.Config();

            // Set using reflection
            var lucaviEnum = "lucavi";
            archerFemaleUnderscore.SetValue(config, lucaviEnum);

            // Verify it was set
            Assert.Equal("lucavi", config.Archer_Female);

            // Test the actual method that's failing
            var configManager = new FFTColorMod.Configuration.ConfigurationManager("dummy_path.json");

            // Check if the SetJobThemeForJob can find the property
            // We can't fully test it without file I/O, but we can check the property lookup
            var prop = typeof(FFTColorMod.Configuration.Config).GetProperty("Archer_Female");
            Assert.NotNull(prop);
            Assert.Equal(typeof(string), prop.PropertyType);
        }
    }
}