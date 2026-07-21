using System;

namespace FFTColorCustomizer.Configuration
{
    /// <summary>
    /// Resolves the mod's actual install directory for sprite operations (CC-12).
    ///
    /// The directory containing the mod's own executing DLL is authoritative: Reloaded-II
    /// loaded the DLL from whatever the install is really called (paxtrick.fft.colorcustomizer,
    /// a Vortex FFTColorCustomizer-56-* name, or a dev FFTColorCustomizer link). The previous
    /// approach composed a hard-coded "Mods\FFTColorCustomizer" path from the User config
    /// location, which pointed at a nonexistent folder for every release install: the config
    /// saved, the UI reported success, and no sprite was ever copied.
    /// </summary>
    public static class ModInstallPathResolver
    {
        public static string Resolve(string? executingAssemblyDir, string? modFolder)
        {
            if (!string.IsNullOrWhiteSpace(executingAssemblyDir))
                return executingAssemblyDir;
            if (!string.IsNullOrWhiteSpace(modFolder))
                return modFolder;
            return string.Empty;
        }
    }
}
