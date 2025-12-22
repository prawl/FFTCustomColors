using System;
using System.IO;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Generates themed tex files for Ramza that can be swapped at runtime
    /// </summary>
    public class RamzaTexGenerator
    {
        private readonly string _basePath;
        private readonly TexFileModifier _texModifier;
        private readonly int[] _ramzaTexNumbers = { 830, 831, 832, 833, 834, 835 };

        public RamzaTexGenerator(string basePath)
        {
            _basePath = basePath;
            _texModifier = new TexFileModifier();
        }

        public void GenerateThemedTexFiles(string themeName)
        {
            string themeDir = Path.Combine(_basePath, themeName);
            Directory.CreateDirectory(themeDir);

            string originalPath = Path.Combine(_basePath, "original_backup");

            // Process each tex file that exists in the original backup
            foreach (int texNumber in _ramzaTexNumbers)
            {
                string inputFile = Path.Combine(originalPath, $"tex_{texNumber}.bin");
                string outputFile = Path.Combine(themeDir, $"tex_{texNumber}.bin");

                if (File.Exists(inputFile))
                {
                    _texModifier.ModifyTexColors(inputFile, outputFile, themeName);
                }
            }
        }
    }
}