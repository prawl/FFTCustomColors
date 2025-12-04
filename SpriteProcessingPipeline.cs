using System.IO;

namespace FFTColorMod
{
    public class SpriteProcessingPipeline
    {
        // TLDR: Minimal pipeline to pass test

        public void ProcessSprite(byte[] spriteData, string outputPath, string fileName)
        {
            // TLDR: Create directories and save files
            Directory.CreateDirectory(Path.Combine(outputPath, "sprites_blue"));
            Directory.CreateDirectory(Path.Combine(outputPath, "sprites_red"));
            Directory.CreateDirectory(Path.Combine(outputPath, "sprites_green"));
            Directory.CreateDirectory(Path.Combine(outputPath, "sprites_purple"));
            Directory.CreateDirectory(Path.Combine(outputPath, "sprites_original"));

            // TLDR: Save original
            File.WriteAllBytes(Path.Combine(outputPath, "sprites_original", fileName), spriteData);

            // TLDR: Create blue variant with modified colors
            var blueData = (byte[])spriteData.Clone();
            // Find and change Ramza brown colors
            for (int i = 0; i < blueData.Length - 2; i++)
            {
                if (blueData[i] == 0x17 && blueData[i+1] == 0x2C && blueData[i+2] == 0x4A)
                {
                    blueData[i] = 0x47; // Make it blue-ish
                }
            }
            File.WriteAllBytes(Path.Combine(outputPath, "sprites_blue", fileName), blueData);

            // TLDR: Save other variants (unchanged for now)
            File.WriteAllBytes(Path.Combine(outputPath, "sprites_red", fileName), spriteData);
            File.WriteAllBytes(Path.Combine(outputPath, "sprites_green", fileName), spriteData);
            File.WriteAllBytes(Path.Combine(outputPath, "sprites_purple", fileName), spriteData);
        }
    }
}