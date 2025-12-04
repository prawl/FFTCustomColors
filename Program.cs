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
    }
}