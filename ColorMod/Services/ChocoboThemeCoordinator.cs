using System;
using System.IO;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.ThemeEditor;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Applies the three chocobo tier color selections by recoloring palettes 0/1/2 of
    /// battle_cyoko_spr.bin (all tiers live in one bin). Always rebuilds from the original copy
    /// in sprites_original/ so re-applies are idempotent and "original" tiers reset cleanly.
    /// Writes to the base unit/ folder (FFTPack serves from there after a restart).
    /// </summary>
    public class ChocoboThemeCoordinator
    {
        private const string ChocoboSprite = "battle_cyoko_spr.bin";
        private readonly string _modPath;
        private readonly string _unitPath;

        public ChocoboThemeCoordinator(string modPath, string unitPath)
        {
            _modPath = modPath;
            _unitPath = unitPath;
        }

        public void Apply(Config config)
        {
            var originalFile = Path.Combine(_unitPath, "sprites_original", ChocoboSprite);
            if (!File.Exists(originalFile))
            {
                ModLogger.LogWarning($"[CHOCOBO] Original sprite not found, skipping: {originalFile}");
                return;
            }

            var mapping = LoadMapping();
            if (mapping == null)
            {
                ModLogger.LogWarning("[CHOCOBO] Chocobo section mapping not found, skipping recolor");
                return;
            }

            var data = File.ReadAllBytes(originalFile);
            var userThemes = new UserThemeService(_modPath);
            bool anyThemed = false;

            foreach (var tierKey in ChocoboThemePresets.TierKeys)
            {
                var themeName = config.GetJobTheme(tierKey) ?? ChocoboThemePresets.Original;
                int paletteIndex = ChocoboThemePresets.PaletteIndexForTier(tierKey);

                if (ChocoboRecolor.ApplyTheme(data, paletteIndex, mapping.Sections, tierKey, themeName, userThemes))
                {
                    anyThemed = true;
                    ModLogger.Log($"[CHOCOBO] {tierKey} -> {themeName} (palette {paletteIndex})");
                }
                else if (!string.IsNullOrEmpty(themeName) && themeName != ChocoboThemePresets.Original)
                {
                    ModLogger.LogWarning($"[CHOCOBO] Unknown theme {tierKey} = {themeName}");
                }
            }

            // Always write: rebuilding from the original means flipping a tier back to "original"
            // restores its palette on the next apply.
            var destFile = Path.Combine(_unitPath, ChocoboSprite);
            File.WriteAllBytes(destFile, data);
            ModLogger.LogSuccess($"[CHOCOBO] Wrote {destFile} (themed: {anyThemed})");
        }

        private SectionMapping LoadMapping()
        {
            foreach (var sub in new[] { "Monster", "Story" })
            {
                var path = Path.Combine(_modPath, "Data", "SectionMappings", sub, "Chocobo.json");
                if (File.Exists(path))
                    return SectionMappingLoader.LoadFromFile(path);
            }
            return null;
        }
    }
}
