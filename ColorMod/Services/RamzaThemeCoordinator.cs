using System;
using System.IO;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Coordinates Ramza tex theme generation and application
    /// </summary>
    public class RamzaThemeCoordinator
    {
        private readonly string _sourcePath;
        private readonly string _modPath;
        private readonly RamzaTexGenerator _texGenerator;

        public RamzaThemeCoordinator(string sourcePath, string modPath)
        {
            _sourcePath = sourcePath;
            _modPath = modPath;

            string basePath = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "system", "ffto", "g2d");
            _texGenerator = new RamzaTexGenerator(basePath);
        }

        public void GenerateAllRamzaThemes(string[] themes)
        {
            foreach (string theme in themes)
            {
                _texGenerator.GenerateThemedTexFiles(theme);
            }
        }

        public void ApplyRamzaTheme(string themeName)
        {
            // RamzaTexSwapper expects mod root path (contains RamzaThemes folder), not g2d path
            string gameBasePath = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "system", "ffto", "g2d");

            var swapper = new RamzaTexSwapper(_sourcePath, gameBasePath);

            // Handle "original" theme specially - restore original tex files instead of swapping
            if (themeName == "original")
            {
                swapper.RestoreOriginalTexFiles();
            }
            else
            {
                swapper.SwapTexFilesForTheme(themeName);
            }
        }
    }
}