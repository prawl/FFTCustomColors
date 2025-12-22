using System;
using System.IO;
using System.Linq;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Service for applying themes to Ramza tex files
    /// </summary>
    public class RamzaTexThemeService
    {
        private readonly TexFileModifier _texModifier;
        private readonly string _basePath;

        // Ramza tex file numbers (one for each chapter)
        private readonly int[] _ramzaTexNumbers = { 830, 831, 832, 833, 834, 835 };

        public RamzaTexThemeService(string basePath)
        {
            _texModifier = new TexFileModifier();
            _basePath = basePath;
        }

        /// <summary>
        /// Applies a theme to all Ramza tex files
        /// </summary>
        public void ApplyThemeToRamzaTexFiles(string themeName)
        {
            string originalPath = Path.Combine(_basePath, "original_backup");
            string outputPath = _basePath;

            // Ensure original backup directory exists
            if (!Directory.Exists(originalPath))
            {
                throw new DirectoryNotFoundException($"Original backup directory not found: {originalPath}");
            }

            int successCount = 0;
            int totalChanges = 0;

            foreach (int texNumber in _ramzaTexNumbers)
            {
                string inputFile = Path.Combine(originalPath, $"tex_{texNumber}.bin");
                string outputFile = Path.Combine(outputPath, $"tex_{texNumber}.bin");

                if (File.Exists(inputFile))
                {
                    try
                    {
                        int changes = _texModifier.ModifyTexColors(inputFile, outputFile, themeName);
                        totalChanges += changes;
                        successCount++;
                        Console.WriteLine($"Applied {themeName} to tex_{texNumber}.bin ({changes} color changes)");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to process tex_{texNumber}.bin: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"Warning: tex_{texNumber}.bin not found in backup directory");
                }
            }

            Console.WriteLine($"Theme {themeName} applied to {successCount}/{_ramzaTexNumbers.Length} Ramza tex files");
            Console.WriteLine($"Total color modifications: {totalChanges}");
        }

        /// <summary>
        /// Restores original Ramza tex files
        /// </summary>
        public void RestoreOriginalTexFiles()
        {
            string originalPath = Path.Combine(_basePath, "original_backup");
            string outputPath = _basePath;

            if (!Directory.Exists(originalPath))
            {
                throw new DirectoryNotFoundException($"Original backup directory not found: {originalPath}");
            }

            int restoredCount = 0;

            foreach (int texNumber in _ramzaTexNumbers)
            {
                string sourceFile = Path.Combine(originalPath, $"tex_{texNumber}.bin");
                string destFile = Path.Combine(outputPath, $"tex_{texNumber}.bin");

                if (File.Exists(sourceFile))
                {
                    try
                    {
                        File.Copy(sourceFile, destFile, overwrite: true);
                        restoredCount++;
                        Console.WriteLine($"Restored original tex_{texNumber}.bin");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to restore tex_{texNumber}.bin: {ex.Message}");
                    }
                }
            }

            Console.WriteLine($"Restored {restoredCount}/{_ramzaTexNumbers.Length} original Ramza tex files");
        }

        /// <summary>
        /// Checks if original backup files exist
        /// </summary>
        public bool HasOriginalBackup()
        {
            string originalPath = Path.Combine(_basePath, "original_backup");

            if (!Directory.Exists(originalPath))
                return false;

            // Check if all tex files exist in backup
            return _ramzaTexNumbers.All(texNum =>
                File.Exists(Path.Combine(originalPath, $"tex_{texNum}.bin")));
        }

        /// <summary>
        /// Creates backup of current tex files if not already backed up
        /// </summary>
        public void CreateBackupIfNeeded()
        {
            string originalPath = Path.Combine(_basePath, "original_backup");

            // Create backup directory if it doesn't exist
            if (!Directory.Exists(originalPath))
            {
                Directory.CreateDirectory(originalPath);
            }

            int backedUpCount = 0;

            foreach (int texNumber in _ramzaTexNumbers)
            {
                string sourceFile = Path.Combine(_basePath, $"tex_{texNumber}.bin");
                string backupFile = Path.Combine(originalPath, $"tex_{texNumber}.bin");

                // Only backup if source exists and backup doesn't
                if (File.Exists(sourceFile) && !File.Exists(backupFile))
                {
                    try
                    {
                        File.Copy(sourceFile, backupFile);
                        backedUpCount++;
                        Console.WriteLine($"Backed up tex_{texNumber}.bin");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to backup tex_{texNumber}.bin: {ex.Message}");
                    }
                }
            }

            if (backedUpCount > 0)
            {
                Console.WriteLine($"Created backup of {backedUpCount} tex files");
            }
        }
    }
}