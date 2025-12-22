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
            string sourceBasePath = Path.Combine(_sourcePath, "FFTIVC", "data", "enhanced", "system", "ffto", "g2d");
            string gameBasePath = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "system", "ffto", "g2d");

            var swapper = new RamzaTexSwapper(sourceBasePath, gameBasePath);
            swapper.SwapTexFilesForTheme(themeName);
        }
    }
}