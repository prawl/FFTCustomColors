using System;
using FFTColorCustomizer.Configuration;
using System.Linq;
using System.Reflection;
using Xunit;

namespace FFTColorCustomizer.Tests
{
    public class CheckReloadedNamespaces
    {
        [Fact]
        public void FindConfigurableClass()
        {
            // Load the Reloaded.Mod.Loader.IO assembly
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.FullName.Contains("Reloaded.Mod.Loader.IO"));

            foreach (var assembly in assemblies)
            {
                Console.WriteLine($"Assembly: {assembly.FullName}");

                // Find all types with "Config" in the name
                var configTypes = assembly.GetTypes()
                    .Where(t => t.Name.Contains("Config"));

                foreach (var type in configTypes)
                {
                    Console.WriteLine($"  Type: {type.FullName}");
                }
            }

            Assert.True(true); // This test is just for investigation
        }
    }
}
