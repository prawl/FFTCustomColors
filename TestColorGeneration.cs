using System;
using System.IO;

namespace FFTColorMod
{
    public class TestColorGeneration
    {
        public static void RunTest(string[] args)
        {
            Console.WriteLine("FFT Color Mod - Generic Sprite Color Generator Test");
            Console.WriteLine("====================================================");

            if (args.Length < 2)
            {
                Console.WriteLine("Usage: TestColorGeneration.exe <sprite_file> <output_dir>");
                return;
            }

            string spriteFile = args[0];
            string outputDir = args[1];

            if (!File.Exists(spriteFile))
            {
                Console.WriteLine($"Sprite file not found: {spriteFile}");
                return;
            }

            Console.WriteLine($"Processing: {spriteFile}");
            Console.WriteLine($"Output to: {outputDir}");

            var generator = new SpriteColorGeneratorV2();
            generator.ProcessSingleSprite(spriteFile, outputDir);

            Console.WriteLine("\nGenerated color variants:");
            Console.WriteLine($"  - {outputDir}/sprites_original/{Path.GetFileName(spriteFile)}");
            Console.WriteLine($"  - {outputDir}/sprites_red/{Path.GetFileName(spriteFile)}");
            Console.WriteLine($"  - {outputDir}/sprites_blue/{Path.GetFileName(spriteFile)}");
            Console.WriteLine($"  - {outputDir}/sprites_green/{Path.GetFileName(spriteFile)}");
            Console.WriteLine($"  - {outputDir}/sprites_purple/{Path.GetFileName(spriteFile)}");

            Console.WriteLine("\nDone! Now comparing palettes to verify changes...");

            // Read and compare first few bytes of palettes
            var original = File.ReadAllBytes(Path.Combine(outputDir, "sprites_original", Path.GetFileName(spriteFile)));
            var red = File.ReadAllBytes(Path.Combine(outputDir, "sprites_red", Path.GetFileName(spriteFile)));

            Console.WriteLine("\nFirst 12 palette bytes comparison (BGR format):");
            Console.WriteLine("Original: " + BitConverter.ToString(original, 0, Math.Min(12, original.Length)));
            Console.WriteLine("Red:      " + BitConverter.ToString(red, 0, Math.Min(12, red.Length)));

            bool different = false;
            for (int i = 0; i < Math.Min(96, Math.Min(original.Length, red.Length)); i++)
            {
                if (original[i] != red[i])
                {
                    different = true;
                    break;
                }
            }

            if (different)
            {
                Console.WriteLine("\n✅ SUCCESS: Color palettes are different!");
            }
            else
            {
                Console.WriteLine("\n❌ PROBLEM: Color palettes are identical - no changes made");
            }
        }
    }
}