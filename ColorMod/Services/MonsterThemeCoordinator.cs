using System;
using System.IO;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.ThemeEditor;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Applies every registered monster family's tier color selections by recoloring palettes
    /// 0/1/2 of the family's sprite bin (all three ranks live in one bin). Always rebuilds each
    /// bin from its original copy in sprites_original/ so re-applies are idempotent and tiers flip
    /// back to "original" cleanly. Writes to the base unit/ folder (FFTPack serves from there after
    /// a restart). See docs/ADDING_A_MONSTER.md.
    /// </summary>
    public class MonsterThemeCoordinator
    {
        private readonly string _modPath;
        private readonly string _unitPath;

        public MonsterThemeCoordinator(string modPath, string unitPath)
        {
            _modPath = modPath;
            _unitPath = unitPath;
        }

        public void Apply(Config config)
        {
            var userThemes = new UserThemeService(_modPath);

            foreach (var family in MonsterThemeRegistry.Families)
            {
                try
                {
                    ApplyFamily(config, family, userThemes);
                }
                catch (Exception ex)
                {
                    ModLogger.LogError($"[MONSTER] {family.Family} apply failed: {ex.Message}");
                }
            }
        }

        private void ApplyFamily(Config config, MonsterFamily family, UserThemeService userThemes)
        {
            var originalFile = Path.Combine(_unitPath, "sprites_original", family.Bin);
            if (!File.Exists(originalFile))
            {
                ModLogger.LogWarning($"[MONSTER] {family.Family}: original sprite not found, skipping: {originalFile}");
                return;
            }

            var mapping = LoadMapping(family.Family);
            if (mapping == null)
            {
                ModLogger.LogWarning($"[MONSTER] {family.Family}: section mapping not found, skipping recolor");
                return;
            }

            var data = File.ReadAllBytes(originalFile);
            bool anyThemed = false;

            for (int ti = 0; ti < family.TierKeys.Length; ti++)
            {
                var tierKey = family.TierKeys[ti];
                var themeName = config.GetJobTheme(tierKey) ?? MonsterThemeRegistry.Original;
                int paletteIndex = family.PaletteIndices[ti];

                if (MonsterRecolor.ApplyTheme(data, paletteIndex, mapping.Sections, tierKey, themeName, userThemes))
                {
                    anyThemed = true;
                    ModLogger.Log($"[MONSTER] {tierKey} -> {themeName} (palette {paletteIndex})");
                }
                else if (!string.IsNullOrEmpty(themeName) && themeName != MonsterThemeRegistry.Original)
                {
                    ModLogger.LogWarning($"[MONSTER] Unknown theme {tierKey} = {themeName}");
                }
            }

            // Always write: rebuilding from the original means flipping a tier back to "original"
            // restores its palette on the next apply.
            var destFile = Path.Combine(_unitPath, family.Bin);
            File.WriteAllBytes(destFile, data);
            ModLogger.LogSuccess($"[MONSTER] {family.Family}: wrote {destFile} (themed: {anyThemed})");
        }

        private SectionMapping LoadMapping(string family)
        {
            foreach (var sub in new[] { "Monster", "Story" })
            {
                var path = Path.Combine(_modPath, "Data", "SectionMappings", sub, $"{family}.json");
                if (File.Exists(path))
                    return SectionMappingLoader.LoadFromFile(path);
            }
            return null;
        }
    }
}
