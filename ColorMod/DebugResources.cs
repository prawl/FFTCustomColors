using System;
using System.Linq;
using System.Reflection;

namespace FFTColorCustomizer.Debug
{
    public static class DebugResources
    {
        public static void ListEmbeddedResources()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resources = assembly.GetManifestResourceNames();

            Console.WriteLine($"Total embedded resources: {resources.Length}");

            var squireOriginal = resources.Where(r => r.Contains("squire_male_original")).ToList();
            Console.WriteLine($"\nSquire Male Original resources ({squireOriginal.Count}):");
            foreach (var resource in squireOriginal.OrderBy(r => r))
            {
                Console.WriteLine($"  - {resource}");
            }

            // Check for directional sprites
            var directionalCount = squireOriginal.Count(r =>
                r.Contains("_n.png") || r.Contains("_ne.png") ||
                r.Contains("_e.png") || r.Contains("_se.png") ||
                r.Contains("_s.png") || r.Contains("_sw.png") ||
                r.Contains("_w.png") || r.Contains("_nw.png"));

            Console.WriteLine($"\nDirectional sprites found: {directionalCount}");
        }
    }
}