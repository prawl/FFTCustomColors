using System;
using System.IO;

namespace FFTColorMod
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("FFT Color Mod - Sprite Processor");
            Console.WriteLine("=====================================");

            if (args.Length == 0)
            {
                ShowUsage();
                return;
            }

            string command = args[0].ToLower();

            switch (command)
            {
                case "extract":
                    ExtractSprites(args);
                    break;

                case "extract-single":
                    ExtractSinglePac(args);
                    break;

                case "process":
                    ProcessSprites(args);
                    break;

                case "full":
                    RunFullPipeline(args);
                    break;

                case "find-ramza":
                    FindRamza(args);
                    break;

                case "binary-search":
                    BinarySearchForRamza(args);
                    break;

                case "list-files":
                    ListPacFiles(args);
                    break;

                case "debug-pac":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("Usage: debug-pac <pac-file>");
                        return;
                    }
                    DebugPacFile(args);
                    break;

                default:
                    Console.WriteLine($"Unknown command: {command}");
                    ShowUsage();
                    break;
            }
        }

        static void ShowUsage()
        {
            Console.WriteLine("Usage: FFTColorMod.exe <command> [options]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  extract               Extract sprites from FFT enhanced PAC files");
            Console.WriteLine("  extract-single <pac> <output>  Extract sprites from a single PAC file");
            Console.WriteLine("  process <sprite> <output>      Process a single sprite to generate color variants");
            Console.WriteLine("  full                 Run full pipeline: extract and process");
            Console.WriteLine("  binary-search        Apply green/red colors for Ramza binary search");
            Console.WriteLine();
            Console.WriteLine("The tool will automatically use FFT's enhanced directory.");
        }

        static void ExtractSprites(string[] args)
        {
            // TLDR: Extract sprites from enhanced FFT PAC files
            string fftPath = @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\data\enhanced";
            string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "extracted_sprites");

            if (!Directory.Exists(fftPath))
            {
                Console.WriteLine($"FFT enhanced directory not found at: {fftPath}");
                return;
            }

            Console.WriteLine($"Extracting sprites from: {fftPath}");
            Console.WriteLine($"Output directory: {outputPath}");

            // Test with just one PAC file first
            if (args.Length > 1 && args[1] == "test")
            {
                Console.WriteLine("TEST MODE: Extracting from 0000.pac only");
                var testPac = Path.Combine(fftPath, "0000.pac");
                if (File.Exists(testPac))
                {
                    var extractor = new PacExtractor();
                    if (extractor.OpenPac(testPac))
                    {
                        Console.WriteLine($"Opened {testPac}");
                        Console.WriteLine($"File count: {extractor.GetFileCount()}");

                        // Extract just first 10 sprites for testing
                        int testCount = Math.Min(10, extractor.GetFileCount());
                        int extracted = 0;
                        for (int i = 0; i < testCount; i++)
                        {
                            var fileName = extractor.GetFileName(i);
                            if (fileName != null &&
                                (fileName.EndsWith(".spr", StringComparison.OrdinalIgnoreCase) ||
                                 fileName.EndsWith("_spr.bin", StringComparison.OrdinalIgnoreCase)))
                            {
                                Console.WriteLine($"  Found sprite: {fileName}");
                                extracted++;
                            }
                        }
                        Console.WriteLine($"Found {extracted} sprites in first {testCount} files");
                    }
                }
                return;
            }

            int totalExtracted = PacExtractor.ExtractSpritesFromDirectory(fftPath, outputPath);
            Console.WriteLine($"\nExtraction complete! Total sprites extracted: {totalExtracted}");
        }

        static void ExtractSinglePac(string[] args)
        {
            // TLDR: Extract sprites from a single PAC file
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: extract-single <pac_file> <output_dir>");
                return;
            }

            string pacFile = args[1];
            string outputPath = args[2];

            if (!File.Exists(pacFile))
            {
                Console.WriteLine($"PAC file not found: {pacFile}");
                return;
            }

            // Create output directory if it doesn't exist
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            Console.WriteLine($"Extracting sprites from: {pacFile}");
            Console.WriteLine($"Output directory: {outputPath}");

            var extractor = new PacExtractor();
            if (extractor.OpenPac(pacFile))
            {
                int extractedCount = extractor.ExtractAllSprites(outputPath);
                Console.WriteLine($"Successfully extracted {extractedCount} sprites");
            }
            else
            {
                Console.WriteLine($"Failed to open PAC file: {pacFile}");
            }
        }

        static void ProcessSprites(string[] args)
        {
            // TLDR: Generate color variants for extracted sprites or a single sprite
            if (args.Length < 3)
            {
                // Directory processing mode
                string inputPath = args.Length > 1 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "extracted_sprites");
                string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "FFTIVC", "data");

                if (!Directory.Exists(inputPath))
                {
                    Console.WriteLine($"Input directory not found: {inputPath}");
                    return;
                }

                Console.WriteLine($"Processing sprites from: {inputPath}");
                Console.WriteLine($"Output directory: {outputPath}");

                var generator = new SpriteColorGenerator();
                var processedCount = generator.ProcessDirectory(inputPath, outputPath);

                Console.WriteLine($"\nProcessing complete! Files processed: {processedCount}");
            }
            else
            {
                // Single sprite processing mode
                string spriteFile = args[1];
                string outputPath = args[2];

                if (!File.Exists(spriteFile))
                {
                    Console.WriteLine($"Sprite file not found: {spriteFile}");
                    return;
                }

                Console.WriteLine($"Processing sprite: {spriteFile}");
                Console.WriteLine($"Output directory: {outputPath}");

                var generator = new SpriteColorGenerator();
                generator.ProcessSingleSprite(spriteFile, outputPath);

                Console.WriteLine($"Color variants generated for: {Path.GetFileName(spriteFile)}");
            }
        }

        static void RunFullPipeline(string[] args)
        {
            // TLDR: Run complete extraction and processing pipeline
            Console.WriteLine("Running full pipeline: Extract -> Process");
            Console.WriteLine();

            ExtractSprites(args);
            Console.WriteLine();
            ProcessSprites(new[] { "process" });
        }

        static void FindRamza(string[] args)
        {
            // TLDR: Find and extract Ramza sprites from PAC files
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: find-ramza <game-dir> <output-dir>");
                return;
            }

            string gameDir = args[1];
            string outputDir = args[2];

            var extractor = new PacExtractor();
            var extracted = extractor.FindAndExtractSpritesUsingStream(gameDir, "ramza", outputDir);

            Console.WriteLine($"Found and extracted {extracted.Count} Ramza sprites");
        }

        static void BinarySearchForRamza(string[] args)
        {
            // TLDR: Apply green/red colors to sprite halves for binary search
            var searcher = new SimplePaletteReplace();
            searcher.ProcessBinarySearch();
        }

        static void ListPacFiles(string[] args)
        {
            // TLDR: List all files in a PAC archive
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: list-files <pac-file>");
                return;
            }

            string pacFile = args[1];
            var extractor = new PacExtractor();

            if (!File.Exists(pacFile))
            {
                Console.WriteLine($"PAC file not found: {pacFile}");
                return;
            }

            if (extractor.OpenPacStream(pacFile))
            {
                var fileCount = extractor.GetFileCountFromStream();
                Console.WriteLine($"PAC contains {fileCount} files:");
                Console.WriteLine();

                int spriteCount = 0;
                for (int i = 0; i < Math.Min(fileCount, 100); i++)
                {
                    var fileName = extractor.GetFileNameFromStream(i);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        Console.WriteLine($"  {i:D4}: {fileName}");
                        if (fileName.Contains("spr", StringComparison.OrdinalIgnoreCase) ||
                            fileName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                        {
                            spriteCount++;
                        }
                    }
                }

                if (fileCount > 100)
                    Console.WriteLine($"  ... and {fileCount - 100} more files");

                Console.WriteLine();
                Console.WriteLine($"Found {spriteCount} potential sprite files");
                extractor.ClosePac();
            }
        }

        static void DebugPacFile(string[] args)
        {
            // TLDR: Debug PAC file format to understand filename encoding
            string pacFile = args[1];

            if (!File.Exists(pacFile))
            {
                Console.WriteLine($"PAC file not found: {pacFile}");
                return;
            }

            Console.WriteLine($"Debugging PAC file: {pacFile}");
            Console.WriteLine();

            using (var fs = new FileStream(pacFile, FileMode.Open, FileAccess.Read))
            {
                // Read header
                byte[] header = new byte[8];
                fs.Read(header, 0, 8);

                // Check if PACK header
                bool isPack = header[0] == 'P' && header[1] == 'A' && header[2] == 'C' && header[3] == 'K';
                Console.WriteLine($"Header: {BitConverter.ToString(header)}");
                Console.WriteLine($"Is PACK format: {isPack}");

                int fileCount = BitConverter.ToInt32(header, 4);
                Console.WriteLine($"File count: {fileCount}");
                Console.WriteLine();

                // Show first 5 entries in detail
                Console.WriteLine("First 5 file entries (raw hex):");
                for (int i = 0; i < Math.Min(5, fileCount); i++)
                {
                    // Each entry is 40 bytes: 4 offset + 4 size + 32 name
                    byte[] entry = new byte[40];
                    fs.Read(entry, 0, 40);

                    int offset = BitConverter.ToInt32(entry, 0);
                    int size = BitConverter.ToInt32(entry, 4);
                    byte[] nameBytes = new byte[32];
                    Array.Copy(entry, 8, nameBytes, 0, 32);

                    Console.WriteLine($"Entry {i:D3}:");
                    Console.WriteLine($"  Offset: {offset:X8} ({offset})  Size: {size:X8} ({size})");
                    Console.WriteLine($"  Name (hex): {BitConverter.ToString(nameBytes)}");

                    // Try different decodings
                    string asciiName = System.Text.Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                    Console.WriteLine($"  Name (ASCII): {asciiName}");

                    // Check if it might be XORed
                    bool allSame = true;
                    for (int j = 1; j < 32; j++)
                    {
                        if (nameBytes[j] != nameBytes[0])
                        {
                            allSame = false;
                            break;
                        }
                    }

                    if (nameBytes[0] == 0xFF && nameBytes[1] == 0xFF)
                    {
                        Console.WriteLine($"  Note: Name appears to be all 0xFF - might be encrypted or unused");
                    }
                    else if (allSame)
                    {
                        Console.WriteLine($"  Note: All bytes are the same ({nameBytes[0]:X2}) - suspicious pattern");
                    }
                    Console.WriteLine();
                }
            }
        }
    }
}