using System;
using System.IO;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Configuration
{
    /// <summary>
    /// Single source of truth for where Config.json lives (CC-13 split-brain fix).
    ///
    /// The Reloaded convention is User/Mods/&lt;namespace&gt;/Config.json, and the launcher's
    /// Configure button always writes there. The F1 flow used to fall back to the mod folder
    /// whenever the User config did not exist YET, so the two flows could write different
    /// files and a later-created User config silently reverted F1 selections. This resolver
    /// converges every flow on the User path, migrating an existing mod-folder config there
    /// once (never overwriting a User config that already exists).
    /// </summary>
    public static class ConfigPathResolver
    {
        public static string ResolveWithMigration(string modPath, string modNamespace, string configFileName)
        {
            try
            {
                var parent = Directory.GetParent(modPath);
                var grandParent = parent != null ? Directory.GetParent(parent.FullName) : null;
                if (grandParent != null)
                {
                    var userConfigPath = Path.Combine(grandParent.FullName, "User", "Mods", modNamespace, configFileName);
                    var modConfigPath = Path.Combine(modPath, configFileName);

                    if (!File.Exists(userConfigPath) && File.Exists(modConfigPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(userConfigPath)!);
                        File.Copy(modConfigPath, userConfigPath, overwrite: false);
                        ModLogger.Log($"Migrated mod-folder config to the User path: {userConfigPath}");
                    }

                    return userConfigPath;
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogWarning($"Could not resolve the User config path ({ex.Message}); using the mod-folder config.");
            }

            return Path.Combine(modPath, configFileName);
        }
    }
}
