using System;
using FFTColorCustomizer.Configuration;
using System.Linq;
using System.Reflection;
using Xunit;
using Reloaded.Mod.Interfaces;

namespace FFTColorCustomizer.Tests
{
    public class InterfaceInspectionTest
    {
        [Fact]
        public void InspectIConfigurableInterface()
        {
            // Get the IConfigurable interface type
            var interfaceType = typeof(IConfigurable);

            Console.WriteLine($"Interface: {interfaceType.FullName}");
            Console.WriteLine($"Is Generic: {interfaceType.IsGenericTypeDefinition}");

            // Get all members
            var members = interfaceType.GetMembers();
            Console.WriteLine($"\nMembers ({members.Length}):");
            foreach (var member in members)
            {
                Console.WriteLine($"  - {member.MemberType}: {member.Name}");

                if (member is PropertyInfo prop)
                {
                    Console.WriteLine($"    Type: {prop.PropertyType.Name}");
                    Console.WriteLine($"    CanRead: {prop.CanRead}, CanWrite: {prop.CanWrite}");
                }

                if (member is MethodInfo method && !method.IsSpecialName)
                {
                    var parameters = method.GetParameters();
                    var paramStr = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    Console.WriteLine($"    Signature: {method.ReturnType.Name} {method.Name}({paramStr})");
                }
            }

            // Check for any generic methods
            var methods = interfaceType.GetMethods();
            foreach (var method in methods.Where(m => m.IsGenericMethodDefinition))
            {
                Console.WriteLine($"\nGeneric Method: {method.Name}");
                var genericParams = method.GetGenericArguments();
                Console.WriteLine($"  Generic Parameters: {string.Join(", ", genericParams.Select(p => p.Name))}");
            }

            // This will always pass - it's just to see the output
            Assert.NotNull(interfaceType);
        }
    }
}
