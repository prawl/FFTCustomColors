using System;
using System.Linq;
using System.Reflection;
using Xunit;
using Reloaded.Mod.Interfaces;

namespace FFTColorMod.Tests
{
    public class CheckModLoaderAPI
    {
        [Fact]
        public void InspectIModLoaderV1()
        {
            var type = typeof(IModLoaderV1);

            Console.WriteLine($"Interface: {type.FullName}");
            Console.WriteLine($"Methods:");

            foreach (var method in type.GetMethods())
            {
                var parameters = method.GetParameters();
                var paramStr = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                Console.WriteLine($"  - {method.ReturnType.Name} {method.Name}({paramStr})");
            }

            // Check for extension methods
            var assembly = type.Assembly;
            var extensionTypes = assembly.GetTypes()
                .Where(t => t.IsSealed && !t.IsGenericType && !t.IsNested);

            foreach (var extType in extensionTypes)
            {
                var extensionMethods = extType.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .Where(m => m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
                    .Where(m => m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == type);

                if (extensionMethods.Any())
                {
                    Console.WriteLine($"\nExtension methods in {extType.Name}:");
                    foreach (var method in extensionMethods)
                    {
                        var parameters = method.GetParameters().Skip(1);
                        var paramStr = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                        Console.WriteLine($"  - {method.ReturnType.Name} {method.Name}({paramStr})");
                    }
                }
            }

            Assert.NotNull(type);
        }
    }
}