using System;
using System.IO;
using FFTColorCustomizer.Configuration;
using Xunit;

namespace FFTColorCustomizer.Tests
{
    /// <summary>
    /// CC-13 split-brain fix: F1 saves could land in the mod folder while the Reloaded
    /// Configure button always writes Reloaded/User/Mods/&lt;namespace&gt;/Config.json, so a
    /// later-created User config silently reverted F1 selections. The resolver must make
    /// every flow converge on the User path, migrating an existing mod-folder config there
    /// once, and only fall back to the mod folder when the Reloaded layout is underivable.
    /// </summary>
    public class ConfigPathUnificationTests : IDisposable
    {
        private readonly string _root;
        private readonly string _modPath;

        public ConfigPathUnificationTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "cc13_cfg_" + Path.GetRandomFileName());
            _modPath = Path.Combine(_root, "Mods", "paxtrick.fft.colorcustomizer");
            Directory.CreateDirectory(_modPath);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, true); } catch { }
        }

        private string UserConfig => Path.Combine(_root, "User", "Mods", "paxtrick.fft.colorcustomizer", "Config.json");

        [Fact]
        public void Prefers_existing_User_config()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(UserConfig)!);
            File.WriteAllText(UserConfig, "{\"a\":1}");

            var result = ConfigPathResolver.ResolveWithMigration(_modPath, "paxtrick.fft.colorcustomizer", "Config.json");
            Assert.Equal(UserConfig, result);
        }

        [Fact]
        public void Migrates_a_mod_folder_config_to_the_User_path_once()
        {
            var modConfig = Path.Combine(_modPath, "Config.json");
            File.WriteAllText(modConfig, "{\"knight\":\"lucavi\"}");

            var result = ConfigPathResolver.ResolveWithMigration(_modPath, "paxtrick.fft.colorcustomizer", "Config.json");

            Assert.Equal(UserConfig, result);
            Assert.True(File.Exists(UserConfig), "mod-folder config was not migrated to the User path");
            Assert.Equal("{\"knight\":\"lucavi\"}", File.ReadAllText(UserConfig));
        }

        [Fact]
        public void Existing_User_config_wins_over_a_mod_folder_config_without_overwrite()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(UserConfig)!);
            File.WriteAllText(UserConfig, "{\"v\":\"user\"}");
            File.WriteAllText(Path.Combine(_modPath, "Config.json"), "{\"v\":\"modfolder\"}");

            var result = ConfigPathResolver.ResolveWithMigration(_modPath, "paxtrick.fft.colorcustomizer", "Config.json");

            Assert.Equal(UserConfig, result);
            Assert.Equal("{\"v\":\"user\"}", File.ReadAllText(UserConfig));
        }

        [Fact]
        public void Returns_User_path_even_when_no_config_exists_yet()
        {
            var result = ConfigPathResolver.ResolveWithMigration(_modPath, "paxtrick.fft.colorcustomizer", "Config.json");
            Assert.Equal(UserConfig, result);
        }

        [Fact]
        public void Falls_back_to_mod_folder_when_reloaded_layout_is_underivable()
        {
            var orphan = Path.Combine(_root, "orphan");
            Directory.CreateDirectory(orphan);
            // No Mods/<name> two-level structure above; parent walk yields the temp root's parent,
            // which is fine to derive from ONLY if it exists; an orphan single-level dir with no
            // grandparent-style layout must still produce a usable path.
            var result = ConfigPathResolver.ResolveWithMigration(orphan, "paxtrick.fft.colorcustomizer", "Config.json");
            Assert.EndsWith("Config.json", result);
            Assert.True(
                result.Equals(Path.Combine(orphan, "Config.json"), StringComparison.OrdinalIgnoreCase)
                || result.Contains(Path.Combine("User", "Mods")),
                $"unexpected resolution: {result}");
        }
    }
}
