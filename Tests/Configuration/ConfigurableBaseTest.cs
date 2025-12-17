using System;
using FFTColorCustomizer.Configuration;
using System.Reflection;
using Xunit;

namespace FFTColorCustomizer.Tests
{
    public class ConfigurableBaseTest
    {
        [Fact]
        public void CheckConfigurableClass()
        {
            // Try to find the Configurable<T> class
            var configurableType = typeof(FFTColorCustomizer.Configuration.Configurable<>);

            Console.WriteLine($"Found type: {configurableType.FullName}");
            Console.WriteLine($"Assembly: {configurableType.Assembly.FullName}");

            // Check its members
            var members = configurableType.GetMembers();
            foreach (var member in members)
            {
                Console.WriteLine($"  - {member.Name} ({member.MemberType})");
            }

            Assert.NotNull(configurableType);
        }
    }
}
