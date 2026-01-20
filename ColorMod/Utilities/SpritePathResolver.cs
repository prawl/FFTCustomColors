using System;
using System.IO;
using FFTColorCustomizer.Core;
using FFTColorCustomizer.Services;

namespace FFTColorCustomizer.Utilities
{
    /// <summary>
    /// Resolves sprite file paths and job name mappings.
    /// Extracted from ConfigBasedSpriteManager to follow Single Responsibility Principle.
    /// </summary>
    public class SpritePathResolver
    {
        private readonly string _modPath;
        private readonly string _unitPath;
        private readonly JobClassDefinitionService _jobClassService;

        public SpritePathResolver(string modPath, JobClassDefinitionService jobClassService = null)
        {
            _modPath = modPath;
            _unitPath = FFTIVCPathResolver.FindUnitPath(modPath);
            _jobClassService = jobClassService ?? JobClassServiceSingleton.Instance;
        }

        /// <summary>
        /// Gets the base unit path for regular jobs.
        /// </summary>
        public string UnitPath => _unitPath;

        /// <summary>
        /// Gets the unit_psp path for WotL jobs (Dark Knight, Onion Knight).
        /// </summary>
        public string UnitPspPath => Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit_psp");

        /// <summary>
        /// Gets the appropriate unit path for a job (unit_psp for WotL jobs, unit for regular jobs).
        /// </summary>
        public string GetUnitPathForJob(string jobName)
        {
            if (IsWotLJob(jobName))
            {
                return UnitPspPath;
            }
            return _unitPath;
        }

        /// <summary>
        /// Checks if a job is a War of the Lions exclusive job (Dark Knight, Onion Knight).
        /// </summary>
        public bool IsWotLJob(string jobName)
        {
            return jobName.StartsWith("DarkKnight", StringComparison.OrdinalIgnoreCase) ||
                   jobName.StartsWith("OnionKnight", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the sprite filename for a job property (e.g., "Knight_Male" -> "battle_knight_m_spr.bin").
        /// Uses JobClassDefinitionService for data-driven lookup with fallback to hardcoded mapping.
        /// </summary>
        public string GetSpriteNameForJob(string jobProperty)
        {
            // Try data-driven lookup first
            var jobClass = _jobClassService?.GetJobClassByName(jobProperty);
            if (jobClass != null && !string.IsNullOrEmpty(jobClass.SpriteName))
            {
                return jobClass.SpriteName;
            }

            // Fallback to hardcoded mapping for backward compatibility
            return GetSpriteNameForJobFallback(jobProperty);
        }

        /// <summary>
        /// Converts job type (e.g., "knight") to job name format (e.g., "Knight_Male") based on sprite name.
        /// </summary>
        public string ConvertJobTypeToJobName(string jobType, string spriteName)
        {
            // Determine gender from sprite name (_m_ = male, _w_ = female)
            var gender = spriteName.Contains("_m_") ? "Male" : "Female";

            // Map compound job names that need special casing
            var properJobName = jobType.ToLower() switch
            {
                "blackmage" => "BlackMage",
                "whitemage" => "WhiteMage",
                "timemage" => "TimeMage",
                "darkknight" => "DarkKnight",
                "onionknight" => "OnionKnight",
                _ => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(jobType)
            };

            return $"{properJobName}_{gender}";
        }

        /// <summary>
        /// Normalizes character names to proper case for user theme lookups.
        /// Handles multi-word names like "ramzachapter4" → "RamzaChapter4".
        /// </summary>
        public string NormalizeCharacterName(string characterName)
        {
            if (string.IsNullOrEmpty(characterName))
                return characterName;

            var lowerName = characterName.ToLower();
            if (lowerName.StartsWith("ramzachapter") || lowerName.StartsWith("ramzach"))
            {
                if (lowerName.Contains("chapter1") || lowerName.EndsWith("ch1"))
                    return "RamzaChapter1";
                if (lowerName.Contains("chapter2") || lowerName.Contains("chapter23") || lowerName.EndsWith("ch2"))
                    return "RamzaChapter23";
                if (lowerName.Contains("chapter4") || lowerName.EndsWith("ch4"))
                    return "RamzaChapter4";
                return "RamzaChapter1";
            }

            return char.ToUpper(characterName[0]) + characterName.Substring(1);
        }

        /// <summary>
        /// Checks if the character name is a Ramza chapter.
        /// </summary>
        public bool IsRamzaChapter(string characterName)
        {
            var lower = characterName.ToLower();
            return lower == "ramzachapter1" || lower == "ramzachapter23" || lower == "ramzachapter4";
        }

        /// <summary>
        /// Checks if the theme is a built-in Ramza theme (dark_knight, white_heretic, crimson_blade).
        /// </summary>
        public bool IsBuiltInRamzaTheme(string themeName)
        {
            var lower = themeName.ToLower();
            return lower == "dark_knight" || lower == "white_heretic" || lower == "crimson_blade";
        }

        /// <summary>
        /// Resolves the path to the original sprite directory.
        /// </summary>
        public string GetOriginalSpriteDirectory(string unitPath = null)
        {
            return Path.Combine(unitPath ?? _unitPath, "sprites_original");
        }

        /// <summary>
        /// Resolves the path to a theme sprite directory.
        /// </summary>
        public string GetThemeSpriteDirectory(string themeName, string unitPath = null)
        {
            return Path.Combine(unitPath ?? _unitPath, $"sprites_{themeName.ToLower()}");
        }

        /// <summary>
        /// Resolves the path to a job-specific theme directory.
        /// </summary>
        public string GetJobSpecificThemeDirectory(string jobType, string themeName, string unitPath = null)
        {
            return Path.Combine(unitPath ?? _unitPath, $"sprites_{jobType}_{themeName.ToLower()}");
        }

        /// <summary>
        /// Converts internal theme name to display name (e.g., "corpse_brigade" -> "Corpse Brigade").
        /// </summary>
        public string ConvertThemeNameToDisplayName(string themeName)
        {
            if (string.IsNullOrEmpty(themeName))
                return "Original";

            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
                themeName.Replace('_', ' ')
            );
        }

        /// <summary>
        /// Fallback hardcoded mapping for backward compatibility.
        /// </summary>
        private string GetSpriteNameForJobFallback(string jobProperty)
        {
            return jobProperty switch
            {
                "Knight_Male" => "battle_knight_m_spr.bin",
                "Knight_Female" => "battle_knight_w_spr.bin",
                "Archer_Male" => "battle_yumi_m_spr.bin",
                "Archer_Female" => "battle_yumi_w_spr.bin",
                "Chemist_Male" => "battle_item_m_spr.bin",
                "Chemist_Female" => "battle_item_w_spr.bin",
                "Monk_Male" => "battle_monk_m_spr.bin",
                "Monk_Female" => "battle_monk_w_spr.bin",
                "WhiteMage_Male" => "battle_siro_m_spr.bin",
                "WhiteMage_Female" => "battle_siro_w_spr.bin",
                "BlackMage_Male" => "battle_kuro_m_spr.bin",
                "BlackMage_Female" => "battle_kuro_w_spr.bin",
                "Thief_Male" => "battle_thief_m_spr.bin",
                "Thief_Female" => "battle_thief_w_spr.bin",
                "Ninja_Male" => "battle_ninja_m_spr.bin",
                "Ninja_Female" => "battle_ninja_w_spr.bin",
                "Squire_Male" => "battle_mina_m_spr.bin",
                "Squire_Female" => "battle_mina_w_spr.bin",
                "TimeMage_Male" => "battle_toki_m_spr.bin",
                "TimeMage_Female" => "battle_toki_w_spr.bin",
                "Summoner_Male" => "battle_syou_m_spr.bin",
                "Summoner_Female" => "battle_syou_w_spr.bin",
                "Samurai_Male" => "battle_samu_m_spr.bin",
                "Samurai_Female" => "battle_samu_w_spr.bin",
                "Dragoon_Male" => "battle_ryu_m_spr.bin",
                "Dragoon_Female" => "battle_ryu_w_spr.bin",
                "Geomancer_Male" => "battle_fusui_m_spr.bin",
                "Geomancer_Female" => "battle_fusui_w_spr.bin",
                "Mystic_Male" => "battle_onmyo_m_spr.bin",
                "Mystic_Female" => "battle_onmyo_w_spr.bin",
                "Mediator_Male" => "battle_waju_m_spr.bin",
                "Mediator_Female" => "battle_waju_w_spr.bin",
                "Dancer_Female" => "battle_odori_w_spr.bin",
                "Bard_Male" => "battle_gin_m_spr.bin",
                "Mime_Male" => "battle_mono_m_spr.bin",
                "Mime_Female" => "battle_mono_w_spr.bin",
                "Calculator_Male" => "battle_san_m_spr.bin",
                "Calculator_Female" => "battle_san_w_spr.bin",
                "DarkKnight_Male" => "spr_dst_bchr_ankoku_m_spr.bin",
                "DarkKnight_Female" => "spr_dst_bchr_ankoku_w_spr.bin",
                "OnionKnight_Male" => "spr_dst_bchr_tama_m_spr.bin",
                "OnionKnight_Female" => "spr_dst_bchr_tama_w_spr.bin",
                _ => null
            };
        }
    }
}
