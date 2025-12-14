using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FFTColorMod
{
    public class PacExtractor
    {
        // TLDR: Extracts sprite files from FFT PAC archives
        private byte[]? _pacData;
        private int _fileCount;
        private FileStream? _pacStream;

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

            // Check for PACK header (FFT format)
            if (_pacData.Length >= 4 &&
                _pacData[0] == 'P' && _pacData[1] == 'A' &&
                _pacData[2] == 'C' && _pacData[3] == 'K')
            {
                // FFT PAC format: PACK header + file count at offset 4
                if (_pacData.Length >= 8)
                {
                    _fileCount = BitConverter.ToInt32(_pacData, 4);
                }
                else
                {
                    _fileCount = 0;
                }
            }
            else if (_pacData.Length >= 4)
            {
                // Simple format: just file count at start
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

            // Try to decode the filename - it might be XORed or use different encoding
            byte[] nameBytes = new byte[32];
            Array.Copy(_pacData, nameOffset, nameBytes, 0, 32);

            // Check if all bytes have the same pattern (potential encryption)
            for (int i = 1; i < nameBytes.Length; i++)
            {
                if (nameBytes[i] != nameBytes[0])
                {
                    break;
                }
            }

            // Try ASCII first (original approach)
            string name = Encoding.ASCII.GetString(nameBytes);

            // If name looks corrupted (lots of ? or non-printable), try other encodings
            int questionCount = name.Count(c => c == '?' || c < 32 || c > 126);
            if (questionCount > name.Length / 2)
            {
                // Try UTF-8
                try
                {
                    name = Encoding.UTF8.GetString(nameBytes);
                }
                catch { }

                // If still bad, try Shift-JIS (common in Japanese games)
                if (name.Count(c => c == '?' || c < 32) > name.Length / 2)
                {
                    try
                    {
                        var shiftJis = Encoding.GetEncoding("shift_jis");
                        name = shiftJis.GetString(nameBytes);
                    }
                    catch { }
                }
            }

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
            // TLDR: Extracts all sprite files (.spr and _spr.bin) from the PAC to a directory
            if (_pacData == null) return 0;

            // Create output directory if it doesn't exist
            Directory.CreateDirectory(outputDirectory);

            int extractedCount = 0;

            for (int i = 0; i < _fileCount; i++)
            {
                string? fileName = GetFileName(i);
                if (fileName != null &&
                    (fileName.EndsWith(".spr", StringComparison.OrdinalIgnoreCase) ||
                     fileName.EndsWith("_spr.bin", StringComparison.OrdinalIgnoreCase)))
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

        public bool OpenPacStream(string path)
        {
            // TLDR: Opens a PAC file using streaming to handle large files
            if (string.IsNullOrEmpty(path)) return false;
            if (!File.Exists(path)) return false;

            try
            {
                _pacStream = new FileStream(path, FileMode.Open, FileAccess.Read);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void ClosePac()
        {
            // TLDR: Closes the PAC file stream
            _pacStream?.Dispose();
            _pacStream = null;
        }

        public int GetFileCountFromStream()
        {
            // TLDR: Get file count from open stream
            if (_pacStream == null) return 0;

            try
            {
                using (var reader = new BinaryReader(_pacStream, System.Text.Encoding.ASCII, true))
                {
                    _pacStream.Seek(0, SeekOrigin.Begin);
                    byte[] header = reader.ReadBytes(4);
                    if (header[0] == 'P' && header[1] == 'A' && header[2] == 'C' && header[3] == 'K')
                    {
                        return reader.ReadInt32();
                    }
                    else
                    {
                        _pacStream.Seek(0, SeekOrigin.Begin);
                        return reader.ReadInt32();
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        public string? GetFileNameFromStream(int index)
        {
            // TLDR: Get filename at index from open stream
            if (_pacStream == null) return null;

            try
            {
                using (var reader = new BinaryReader(_pacStream, System.Text.Encoding.ASCII, true))
                {
                    _pacStream.Seek(0, SeekOrigin.Begin);

                    int headerOffset = 0;
                    byte[] header = reader.ReadBytes(4);
                    if (header[0] == 'P' && header[1] == 'A' && header[2] == 'C' && header[3] == 'K')
                    {
                        reader.ReadInt32(); // file count
                        headerOffset = 8;
                    }
                    else
                    {
                        headerOffset = 4;
                    }

                    // Seek to the file entry
                    _pacStream.Seek(headerOffset + (index * 40) + 8, SeekOrigin.Begin);
                    byte[] nameBytes = reader.ReadBytes(32);
                    return System.Text.Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                }
            }
            catch
            {
                return null;
            }
        }

        public List<string> FindAndExtractSprites(string gameDirectory, string searchPattern, string outputDirectory)
        {
            // TLDR: Search for and extract specific sprites from game PAC files
            var extractedFiles = new List<string>();

            if (!Directory.Exists(gameDirectory))
                return extractedFiles;

            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            var pacFiles = Directory.GetFiles(gameDirectory, "*.pac", SearchOption.AllDirectories);

            foreach (var pacFile in pacFiles)
            {
                if (OpenPacStream(pacFile))
                {
                    var fileCount = GetFileCount();
                    for (int i = 0; i < fileCount; i++)
                    {
                        var fileName = GetFileName(i);
                        if (fileName != null &&
                            fileName.Contains(searchPattern, StringComparison.OrdinalIgnoreCase) &&
                            (fileName.EndsWith("_spr.bin", StringComparison.OrdinalIgnoreCase) ||
                             fileName.EndsWith(".spr", StringComparison.OrdinalIgnoreCase)))
                        {
                            var fileData = ExtractFile(i);
                            if (fileData != null)
                            {
                                var outputPath = Path.Combine(outputDirectory, fileName);
                                File.WriteAllBytes(outputPath, fileData);
                                extractedFiles.Add(fileName);
                            }
                        }
                    }
                    ClosePac();
                }
            }

            return extractedFiles;
        }

        public List<string> FindAndExtractSpritesUsingStream(string gameDirectory, string searchPattern, string outputDirectory)
        {
            // TLDR: Search for and extract specific sprites from game PAC files using streaming
            var extractedFiles = new List<string>();

            if (!Directory.Exists(gameDirectory))
                return extractedFiles;

            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            var pacFiles = Directory.GetFiles(gameDirectory, "*.pac", SearchOption.AllDirectories);

            foreach (var pacFile in pacFiles)
            {
                if (OpenPacStream(pacFile))
                {
                    try
                    {
                        using (var reader = new BinaryReader(_pacStream, Encoding.ASCII, true))
                        {
                            _pacStream.Seek(0, SeekOrigin.Begin);

                            int fileCount;
                            int headerOffset = 0;

                            // Check for PACK header
                            byte[] header = reader.ReadBytes(4);
                            if (header[0] == 'P' && header[1] == 'A' && header[2] == 'C' && header[3] == 'K')
                            {
                                fileCount = reader.ReadInt32();
                                headerOffset = 8;
                            }
                            else
                            {
                                _pacStream.Seek(0, SeekOrigin.Begin);
                                fileCount = reader.ReadInt32();
                                headerOffset = 4;
                            }

                            // Read each file entry and check if it matches
                            _pacStream.Seek(headerOffset, SeekOrigin.Begin);
                            for (int i = 0; i < fileCount; i++)
                            {
                                int offset = reader.ReadInt32();
                                int size = reader.ReadInt32();
                                byte[] nameBytes = reader.ReadBytes(32);
                                string fileName = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');

                                // Check if this sprite matches our search pattern
                                if (fileName.Contains(searchPattern, StringComparison.OrdinalIgnoreCase) &&
                                    (fileName.EndsWith("_spr.bin", StringComparison.OrdinalIgnoreCase) ||
                                     fileName.EndsWith(".spr", StringComparison.OrdinalIgnoreCase)))
                                {
                                    // Save current position
                                    long currentPos = _pacStream.Position;

                                    // Extract the file
                                    _pacStream.Seek(offset, SeekOrigin.Begin);
                                    byte[] fileData = reader.ReadBytes(size);

                                    // Write to output
                                    var outputPath = Path.Combine(outputDirectory, fileName);
                                    File.WriteAllBytes(outputPath, fileData);
                                    extractedFiles.Add(fileName);

                                    // Return to next entry position
                                    _pacStream.Seek(currentPos, SeekOrigin.Begin);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore errors and continue with next PAC file
                    }
                    finally
                    {
                        ClosePac();
                    }
                }
            }

            return extractedFiles;
        }

        public int ExtractSpritesUsingStream(string outputDirectory)
        {
            // TLDR: Extract sprites using streaming to handle large files
            if (_pacStream == null) return 0;

            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            int extractedCount = 0;

            try
            {
                using (var reader = new BinaryReader(_pacStream, Encoding.ASCII, true))
                {
                    _pacStream.Seek(0, SeekOrigin.Begin);

                    int fileCount;
                    int headerOffset = 0;

                    // Check for PACK header
                    byte[] header = reader.ReadBytes(4);
                    if (header[0] == 'P' && header[1] == 'A' && header[2] == 'C' && header[3] == 'K')
                    {
                        // FFT PACK format - file count is at offset 4
                        fileCount = reader.ReadInt32();
                        headerOffset = 8; // Start of file entries
                    }
                    else
                    {
                        // Simple format - file count was at start
                        _pacStream.Seek(0, SeekOrigin.Begin);
                        fileCount = reader.ReadInt32();
                        headerOffset = 4; // Start of file entries
                    }

                    // Read each file entry
                    _pacStream.Seek(headerOffset, SeekOrigin.Begin);
                    for (int i = 0; i < fileCount; i++)
                    {
                        int offset = reader.ReadInt32();
                        int size = reader.ReadInt32();
                        byte[] nameBytes = reader.ReadBytes(32);
                        string fileName = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');

                        // Check if it's a sprite file
                        if (fileName.EndsWith(".spr", StringComparison.OrdinalIgnoreCase) ||
                            fileName.EndsWith("_spr.bin", StringComparison.OrdinalIgnoreCase))
                        {
                            // Save current position
                            long currentPos = _pacStream.Position;

                            // Seek to file data
                            _pacStream.Seek(offset, SeekOrigin.Begin);
                            byte[] fileData = reader.ReadBytes(size);

                            // Write to output
                            string outputPath = Path.Combine(outputDirectory, fileName);
                            File.WriteAllBytes(outputPath, fileData);
                            extractedCount++;

                            // Restore position
                            _pacStream.Seek(currentPos, SeekOrigin.Begin);
                        }
                    }
                }
            }
            catch
            {
                // Return what we extracted so far
            }

            return extractedCount;
        }
    }
}