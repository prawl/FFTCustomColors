using System;
using System.Linq;
using System.Reflection;

namespace FFTColorCustomizer.Debug
{
    /// <summary>Lists the embedded resources of the mod assembly for debugging.</summary>
    public static class DebugResources
    {
        public static void ListEmbeddedResources()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resources = assembly.GetManifestResourceNames();

            ModLogger.Debug(LogVerb.Trace, $"Total embedded resources: {resources.Length}");

            var squireOriginal = resources.Where(r => r.Contains("squire_male_original")).ToList();
            ModLogger.Debug(LogVerb.Trace, $"Squire Male Original resources ({squireOriginal.Count})");
            foreach (var resource in squireOriginal.OrderBy(r => r))
            {
                ModLogger.Debug(LogVerb.Trace, $"Resource {resource}");
            }

            // Check for directional sprites
            var directionalCount = squireOriginal.Count(r =>
                r.Contains("_n.png") || r.Contains("_ne.png") ||
                r.Contains("_e.png") || r.Contains("_se.png") ||
                r.Contains("_s.png") || r.Contains("_sw.png") ||
                r.Contains("_w.png") || r.Contains("_nw.png"));

            ModLogger.Debug(LogVerb.Trace, $"Directional sprites found {directionalCount}");
        }
    }
}