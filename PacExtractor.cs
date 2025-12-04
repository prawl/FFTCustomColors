using System;
using System.IO;
using System.Text;

namespace FFTColorMod
{
    public class PacExtractor
    {
        // TLDR: Extracts sprite files from FFT PAC archives
        private byte[]? _pacData;
        private int _fileCount;

        // PAC file structure constants
        private const int FILE_ENTRY_SIZE = 40; // 4 bytes offset + 4 bytes size + 32 bytes name
        private const int HEADER_SIZE = 4; // 4 bytes for file count

        public bool OpenPac(string path)
        {
            // TLDR: Opens a PAC file for extraction
            if (string.IsNullOrEmpty(path)) return false;
            if (!File.Exists(path)) return false;

            // Read PAC file
            _pacData = File.ReadAllBytes(path);

            // Ensure we have at least 4 bytes for file count
            if (_pacData.Length >= 4)
            {
                // Read file count from first 4 bytes (little endian)
                _fileCount = BitConverter.ToInt32(_pacData, 0);
            }
            else
            {
                _fileCount = 0;
            }

            return true;
        }

        public int GetFileCount()
        {
            // TLDR: Returns number of files in the PAC
            return _fileCount;
        }

        public string? GetFileName(int index)
        {
            // TLDR: Returns name of file at index
            if (_pacData == null || index < 0 || index >= _fileCount)
                return null;

            // Calculate position of file entry
            int entryOffset = HEADER_SIZE + (index * FILE_ENTRY_SIZE);

            // Skip offset and size (8 bytes), read name (32 bytes)
            int nameOffset = entryOffset + 8;

            // Read name and trim null characters
            string name = Encoding.ASCII.GetString(_pacData, nameOffset, 32);
            int nullIndex = name.IndexOf('\0');
            if (nullIndex >= 0)
                name = name.Substring(0, nullIndex);

            return name;
        }

        public int GetFileSize(int index)
        {
            // TLDR: Returns size of file at index
            if (_pacData == null || index < 0 || index >= _fileCount)
                return 0;

            // Calculate position of file entry
            int entryOffset = HEADER_SIZE + (index * FILE_ENTRY_SIZE);

            // Read size (4 bytes after offset)
            int size = BitConverter.ToInt32(_pacData, entryOffset + 4);
            return size;
        }

        public byte[]? ExtractFile(int index)
        {
            // TLDR: Extracts file data at index
            if (_pacData == null || index < 0 || index >= _fileCount)
                return null;

            // Calculate position of file entry
            int entryOffset = HEADER_SIZE + (index * FILE_ENTRY_SIZE);

            // Read offset and size
            int dataOffset = BitConverter.ToInt32(_pacData, entryOffset);
            int size = BitConverter.ToInt32(_pacData, entryOffset + 4);

            // Extract file data
            byte[] fileData = new byte[size];
            Array.Copy(_pacData, dataOffset, fileData, 0, size);

            return fileData;
        }

        public int ExtractAllSprites(string outputDirectory)
        {
            // TLDR: Extracts all .SPR files from the PAC to a directory
            if (_pacData == null) return 0;

            // Create output directory if it doesn't exist
            Directory.CreateDirectory(outputDirectory);

            int extractedCount = 0;

            for (int i = 0; i < _fileCount; i++)
            {
                string? fileName = GetFileName(i);
                if (fileName != null && fileName.EndsWith(".spr", StringComparison.OrdinalIgnoreCase))
                {
                    byte[]? fileData = ExtractFile(i);
                    if (fileData != null)
                    {
                        string outputPath = Path.Combine(outputDirectory, fileName);
                        File.WriteAllBytes(outputPath, fileData);
                        extractedCount++;
                    }
                }
            }

            return extractedCount;
        }

        // Helper method to extract sprites from all PAC files in a directory
        public static int ExtractSpritesFromDirectory(string pacDirectory, string outputDirectory)
        {
            // TLDR: Extracts all sprites from all PAC files in a directory
            if (!Directory.Exists(pacDirectory))
                return 0;

            int totalExtracted = 0;
            var pacFiles = Directory.GetFiles(pacDirectory, "*.pac", SearchOption.AllDirectories);

            foreach (var pacFile in pacFiles)
            {
                var extractor = new PacExtractor();
                if (extractor.OpenPac(pacFile))
                {
                    string pacName = Path.GetFileNameWithoutExtension(pacFile);
                    string pacOutputDir = Path.Combine(outputDirectory, pacName);

                    int extracted = extractor.ExtractAllSprites(pacOutputDir);
                    if (extracted > 0)
                    {
                        Console.WriteLine($"Extracted {extracted} sprites from {Path.GetFileName(pacFile)} to {pacOutputDir}");
                        totalExtracted += extracted;
                    }
                }
            }

            return totalExtracted;
        }
    }
}