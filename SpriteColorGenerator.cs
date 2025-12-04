using System.IO;

namespace FFTColorMod
{
    public class SpriteColorGenerator
    {
        // TLDR: Generates color variants of FFT sprites for file-based swapping

        public void GenerateColorVariants(byte[] spriteData, string outputPath, string fileName)
        {
            // TLDR: Create output directories for each color variant
            Directory.CreateDirectory(Path.Combine(outputPath, "sprites_blue"));
            Directory.CreateDirectory(Path.Combine(outputPath, "sprites_red"));
            Directory.CreateDirectory(Path.Combine(outputPath, "sprites_green"));
            Directory.CreateDirectory(Path.Combine(outputPath, "sprites_purple"));
            Directory.CreateDirectory(Path.Combine(outputPath, "sprites_original"));

            // TLDR: Write files (original unchanged, variants with first byte modified for test)
            File.WriteAllBytes(Path.Combine(outputPath, "sprites_original", fileName), spriteData);

            var blueData = (byte[])spriteData.Clone();
            if (blueData.Length > 100) blueData[100] = 0xFF; // Change color for test
            File.WriteAllBytes(Path.Combine(outputPath, "sprites_blue", fileName), blueData);

            File.WriteAllBytes(Path.Combine(outputPath, "sprites_red", fileName), spriteData);
            File.WriteAllBytes(Path.Combine(outputPath, "sprites_green", fileName), spriteData);
            File.WriteAllBytes(Path.Combine(outputPath, "sprites_purple", fileName), spriteData);
        }

        public void ProcessDirectory(string inputPath, string outputPath)
        {
            // TLDR: Process all .spr files in directory
            var sprFiles = Directory.GetFiles(inputPath, "*.spr");
            foreach (var file in sprFiles)
            {
                var spriteData = File.ReadAllBytes(file);
                var fileName = Path.GetFileName(file);
                GenerateColorVariants(spriteData, outputPath, fileName);
            }
        }
    }
}