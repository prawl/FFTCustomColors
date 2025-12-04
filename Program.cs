using System;
using System.IO;

namespace FFTColorMod
{
    class Program
    {
        static void Main(string[] args)
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

                case "process":
                    ProcessSprites(args);
                    break;

                case "full":
                    RunFullPipeline(args);
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
            Console.WriteLine("  process <dir>        Process extracted sprites to generate color variants");
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
                            if (fileName != null && fileName.EndsWith(".spr", StringComparison.OrdinalIgnoreCase))
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

        static void ProcessSprites(string[] args)
        {
            // TLDR: Generate color variants for extracted sprites
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

        static void RunFullPipeline(string[] args)
        {
            // TLDR: Run complete extraction and processing pipeline
            Console.WriteLine("Running full pipeline: Extract -> Process");
            Console.WriteLine();

            ExtractSprites(args);
            Console.WriteLine();
            ProcessSprites(new[] { "process" });
        }
    }
}