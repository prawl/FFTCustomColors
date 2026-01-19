using System;
using System.IO;
using System.Text.Json;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Saves Ramza theme data by patching the charclut.nxd file directly.
    /// This service handles the conversion from edited BIN palette to NXD format.
    /// </summary>
    public class RamzaThemeSaver
    {
        private readonly RamzaBinToNxdBridge _bridge;

        public RamzaThemeSaver()
        {
            _bridge = new RamzaBinToNxdBridge();
        }

        public RamzaThemeSaver(RamzaBinToNxdBridge bridge)
        {
            _bridge = bridge;
        }

        /// <summary>
        /// Checks if the job name represents a Ramza chapter.
        /// Accepts both short (RamzaCh1) and long (RamzaChapter1) formats.
        /// </summary>
        public bool IsRamzaChapter(string jobName)
        {
            return jobName == "RamzaCh1" || jobName == "RamzaCh23" || jobName == "RamzaCh4" ||
                   jobName == "RamzaChapter1" || jobName == "RamzaChapter23" || jobName == "RamzaChapter4";
        }

        /// <summary>
        /// Gets the chapter number from a Ramza job name.
        /// Accepts both short (RamzaCh1) and long (RamzaChapter1) formats.
        /// </summary>
        public int GetChapterFromJobName(string jobName)
        {
            return jobName switch
            {
                "RamzaCh1" or "RamzaChapter1" => 1,
                "RamzaCh23" or "RamzaChapter23" => 2,
                "RamzaCh4" or "RamzaChapter4" => 4,
                _ => throw new ArgumentException($"Job '{jobName}' is not a Ramza chapter")
            };
        }

        /// <summary>
        /// Normalizes Ramza job name to the canonical format (RamzaChapter1, RamzaChapter23, RamzaChapter4).
        /// This is used for consistent storage in UserThemes registry.
        /// </summary>
        public string NormalizeJobName(string jobName)
        {
            return jobName switch
            {
                "RamzaCh1" => "RamzaChapter1",
                "RamzaCh23" => "RamzaChapter23",
                "RamzaCh4" => "RamzaChapter4",
                _ => jobName // Already in canonical format or not a Ramza job
            };
        }

        /// <summary>
        /// Converts palette bytes (from SPR file) to CLUTData format.
        /// </summary>
        public int[] ConvertPaletteToClutData(byte[] paletteBytes)
        {
            return _bridge.ConvertBinPaletteToClutData(paletteBytes);
        }

        /// <summary>
        /// Creates a JSON array string from CLUTData.
        /// </summary>
        public string CreateClutDataJson(int[] clutData)
        {
            return JsonSerializer.Serialize(clutData);
        }

        /// <summary>
        /// Gets the deployment path for charclut.nxd.
        /// </summary>
        public string GetNxdDeploymentPath(string modPath)
        {
            return Path.Combine(modPath, "FFTIVC", "data", "enhanced", "nxd", "charclut.nxd");
        }

        /// <summary>
        /// Gets the NXD Key and Key2 values for a chapter.
        /// Key maps: Ch1=1, Ch2/3=2, Ch4=3
        /// Key2 is always 0 for the base variant.
        /// </summary>
        public (int key, int key2) GetNxdKeyAndKey2(int chapter)
        {
            int key = chapter switch
            {
                1 => 1,
                2 => 2,
                4 => 3,
                _ => throw new ArgumentException($"Invalid chapter: {chapter}")
            };

            return (key, 0); // Key2=0 for base variant
        }

        /// <summary>
        /// Gets the path to the base NXD template file bundled with the mod.
        /// </summary>
        public string GetBaseNxdPath()
        {
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var assemblyDir = Path.GetDirectoryName(assemblyLocation) ?? "";
            return Path.Combine(assemblyDir, "Data", "nxd", "charclut.nxd");
        }

        /// <summary>
        /// Saves a Ramza theme by converting the palette and patching the NXD file directly.
        /// </summary>
        /// <param name="jobName">Ramza job name (RamzaCh1, RamzaCh23, RamzaCh4)</param>
        /// <param name="paletteData">Palette bytes from the edited SPR file</param>
        /// <param name="modPath">Path to the mod directory</param>
        /// <returns>True if save was successful</returns>
        public bool SaveTheme(string jobName, byte[] paletteData, string modPath)
        {
            if (!IsRamzaChapter(jobName))
                throw new ArgumentException($"Job '{jobName}' is not a Ramza chapter");

            var chapter = GetChapterFromJobName(jobName);
            var clutData = ConvertPaletteToClutData(paletteData);

            return ApplyClutData(chapter, clutData, modPath);
        }

        /// <summary>
        /// Applies a pre-computed CLUTData palette for a specific chapter.
        /// Patches the NXD file directly without using SQLite.
        /// </summary>
        /// <param name="chapter">Chapter number (1, 2, or 4)</param>
        /// <param name="clutData">Pre-computed CLUTData array (48 integers)</param>
        /// <param name="modPath">Path to the mod directory</param>
        /// <returns>True if the NXD was successfully patched</returns>
        public bool ApplyClutData(int chapter, int[] clutData, string modPath)
        {
            var clutDataJson = CreateClutDataJson(clutData);
            var (key, key2) = GetNxdKeyAndKey2(chapter);

            var baseNxdPath = GetBaseNxdPath();
            var deployNxdPath = GetNxdDeploymentPath(modPath);

            // Ensure the deployment directory exists
            var deployDir = Path.GetDirectoryName(deployNxdPath);
            if (!string.IsNullOrEmpty(deployDir) && !Directory.Exists(deployDir))
            {
                Directory.CreateDirectory(deployDir);
            }

            // If deploy NXD doesn't exist yet, copy from base template
            if (!File.Exists(deployNxdPath) && File.Exists(baseNxdPath))
            {
                File.Copy(baseNxdPath, deployNxdPath);
            }

            // Patch the NXD file directly
            var patcher = new NxdPatcher();
            return patcher.PatchSingleEntry(deployNxdPath, key, key2, clutDataJson);
        }

        /// <summary>
        /// Applies CLUTData for all chapters at once.
        /// Used when applying a theme that affects all Ramza chapters.
        /// </summary>
        /// <param name="chapter1ClutData">CLUTData for Chapter 1 (or null to skip)</param>
        /// <param name="chapter23ClutData">CLUTData for Chapter 2/3 (or null to skip)</param>
        /// <param name="chapter4ClutData">CLUTData for Chapter 4 (or null to skip)</param>
        /// <param name="modPath">Path to the mod directory</param>
        /// <returns>True if all updates were successful</returns>
        public bool ApplyAllChaptersClutData(int[]? chapter1ClutData, int[]? chapter23ClutData, int[]? chapter4ClutData, string modPath)
        {
            var baseNxdPath = GetBaseNxdPath();
            var deployNxdPath = GetNxdDeploymentPath(modPath);

            // Ensure the deployment directory exists
            var deployDir = Path.GetDirectoryName(deployNxdPath);
            if (!string.IsNullOrEmpty(deployDir) && !Directory.Exists(deployDir))
            {
                Directory.CreateDirectory(deployDir);
            }

            // If deploy NXD doesn't exist yet, copy from base template
            if (!File.Exists(deployNxdPath) && File.Exists(baseNxdPath))
            {
                File.Copy(baseNxdPath, deployNxdPath);
            }

            var patcher = new NxdPatcher();
            bool allSuccessful = true;

            if (chapter1ClutData != null)
            {
                var json = CreateClutDataJson(chapter1ClutData);
                var (key, key2) = GetNxdKeyAndKey2(1);
                allSuccessful &= patcher.PatchSingleEntry(deployNxdPath, key, key2, json);
            }

            if (chapter23ClutData != null)
            {
                var json = CreateClutDataJson(chapter23ClutData);
                var (key, key2) = GetNxdKeyAndKey2(2);
                allSuccessful &= patcher.PatchSingleEntry(deployNxdPath, key, key2, json);
            }

            if (chapter4ClutData != null)
            {
                var json = CreateClutDataJson(chapter4ClutData);
                var (key, key2) = GetNxdKeyAndKey2(4);
                allSuccessful &= patcher.PatchSingleEntry(deployNxdPath, key, key2, json);
            }

            return allSuccessful;
        }
    }
}
