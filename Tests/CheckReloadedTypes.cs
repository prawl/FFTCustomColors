using System;
using FFTColorMod.Configuration;
using System.Linq;
using Xunit;

namespace FFTColorMod.Tests
{
    public class CheckReloadedTypes
    {
        [Fact]
        public void FindConfigurableTypes()
        {
            // Check what's available in the Reloaded.Mod.Interfaces assembly
            var assembly = typeof(Reloaded.Mod.Interfaces.IConfiguratorV3).Assembly;

            Console.WriteLine($"Assembly: {assembly.FullName}");
            Console.WriteLine("\nTypes containing 'Config':");

            var configTypes = assembly.GetTypes()
                .Where(t => t.Name.Contains("Config") || t.Name.Contains("Updatable"))
                .OrderBy(t => t.FullName);

            foreach (var type in configTypes)
            {
                Console.WriteLine($"  {type.FullName}");
            }

            Assert.NotNull(assembly);
        }
    }
}