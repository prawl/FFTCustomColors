using System;
using System.IO;

namespace FFTColorMod
{
    public class RunColorTest
    {
        public static void TestKnightSprite()
        {
            var spriteFile = "input_sprites/battle_knight_m_spr.bin";
            var outputDir = "test_output";

            Console.WriteLine("Testing Generic Color Generation for Knight Sprite");
            Console.WriteLine("==================================================");
            Console.WriteLine($"Input: {spriteFile}");
            Console.WriteLine($"Output: {outputDir}");

            var generator = new SpriteColorGeneratorV2();
            generator.ProcessSingleSprite(spriteFile, outputDir);

            // Compare files
            var original = File.ReadAllBytes(Path.Combine(outputDir, "sprites_original", Path.GetFileName(spriteFile)));
            var red = File.ReadAllBytes(Path.Combine(outputDir, "sprites_red", Path.GetFileName(spriteFile)));
            var blue = File.ReadAllBytes(Path.Combine(outputDir, "sprites_blue", Path.GetFileName(spriteFile)));

            Console.WriteLine("\nFirst 48 bytes comparison (16 colors in BGR format):");
            Console.WriteLine("Original: " + BitConverter.ToString(original, 0, Math.Min(48, original.Length)));
            Console.WriteLine("Red:      " + BitConverter.ToString(red, 0, Math.Min(48, red.Length)));
            Console.WriteLine("Blue:     " + BitConverter.ToString(blue, 0, Math.Min(48, blue.Length)));

            // Check for differences
            bool redDifferent = false, blueDifferent = false;
            for (int i = 0; i < Math.Min(96, original.Length); i++)
            {
                if (i < red.Length && original[i] != red[i] && !redDifferent)
                {
                    redDifferent = true;
                    Console.WriteLine($"\nRed - First difference at byte {i}: {original[i]:X2} -> {red[i]:X2}");
                }
                if (i < blue.Length && original[i] != blue[i] && !blueDifferent)
                {
                    blueDifferent = true;
                    Console.WriteLine($"Blue - First difference at byte {i}: {original[i]:X2} -> {blue[i]:X2}");
                }
            }

            Console.WriteLine($"\n{(redDifferent ? "✅" : "❌")} Red variant: {(redDifferent ? "Different" : "Identical")}");
            Console.WriteLine($"{(blueDifferent ? "✅" : "❌")} Blue variant: {(blueDifferent ? "Different" : "Identical")}");

            if (redDifferent && blueDifferent)
            {
                Console.WriteLine("\n✅ SUCCESS: Color variants are working!");

                // Copy to mod directory for F1 key testing
                var modPath = @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\Reloaded\Mods\FFT_Color_Mod\FFTIVC\data\enhanced\fftpack\unit\sprites_red";
                Directory.CreateDirectory(modPath);
                File.Copy(Path.Combine(outputDir, "sprites_red", Path.GetFileName(spriteFile)),
                         Path.Combine(modPath, Path.GetFileName(spriteFile)), true);
                Console.WriteLine($"\nCopied red variant to: {modPath}");
            }
            else
            {
                Console.WriteLine("\n❌ FAILED: Color variants not working properly");
            }
        }
    }
}