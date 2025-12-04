using System.IO;

namespace FFTColorMod
{
    public class SpriteColorGenerator
    {
        // TLDR: Generates color variants of FFT sprites for file-based swapping
        private readonly PaletteDetector _detector;

        public SpriteColorGenerator()
        {
            _detector = new PaletteDetector();
        }

        public void GenerateColorVariants(byte[] spriteData, string outputPath, string fileName)
        {
            // TLDR: Create output directories for each color variant
            Directory.CreateDirectory(Path.Combine(outputPath, "sprites_blue"));
            Directory.CreateDirectory(Path.Combine(outputPath, "sprites_red"));
            Directory.CreateDirectory(Path.Combine(outputPath, "sprites_green"));
            Directory.CreateDirectory(Path.Combine(outputPath, "sprites_purple"));
            Directory.CreateDirectory(Path.Combine(outputPath, "sprites_original"));

            // TLDR: Write original unchanged
            File.WriteAllBytes(Path.Combine(outputPath, "sprites_original", fileName), spriteData);

            // TLDR: Try to find and replace palettes in sprite data
            // First, see if we can find a palette
            int paletteOffset = _detector.FindPalette(spriteData);

            if (paletteOffset >= 0)
            {
                // TLDR: Generate color variants using PaletteDetector
                var blueData = (byte[])spriteData.Clone();
                _detector.ReplacePaletteColors(blueData, paletteOffset, "blue");
                File.WriteAllBytes(Path.Combine(outputPath, "sprites_blue", fileName), blueData);

                var redData = (byte[])spriteData.Clone();
                _detector.ReplacePaletteColors(redData, paletteOffset, "red");
                File.WriteAllBytes(Path.Combine(outputPath, "sprites_red", fileName), redData);

                var greenData = (byte[])spriteData.Clone();
                _detector.ReplacePaletteColors(greenData, paletteOffset, "green");
                File.WriteAllBytes(Path.Combine(outputPath, "sprites_green", fileName), greenData);

                var purpleData = (byte[])spriteData.Clone();
                _detector.ReplacePaletteColors(purpleData, paletteOffset, "purple");
                File.WriteAllBytes(Path.Combine(outputPath, "sprites_purple", fileName), purpleData);
            }
            else
            {
                // No palette found - just copy original
                File.WriteAllBytes(Path.Combine(outputPath, "sprites_blue", fileName), spriteData);
                File.WriteAllBytes(Path.Combine(outputPath, "sprites_red", fileName), spriteData);
                File.WriteAllBytes(Path.Combine(outputPath, "sprites_green", fileName), spriteData);
                File.WriteAllBytes(Path.Combine(outputPath, "sprites_purple", fileName), spriteData);
            }
        }

        public int ProcessDirectory(string inputPath, string outputPath)
        {
            // TLDR: Process all sprite files (.spr and _spr.bin) in directory and return count
            var sprFiles = Directory.GetFiles(inputPath, "*.spr", SearchOption.AllDirectories);
            var binFiles = Directory.GetFiles(inputPath, "*_spr.bin", SearchOption.AllDirectories);

            foreach (var file in sprFiles)
            {
                var spriteData = File.ReadAllBytes(file);
                var fileName = Path.GetFileName(file);
                GenerateColorVariants(spriteData, outputPath, fileName);
            }

            foreach (var file in binFiles)
            {
                var spriteData = File.ReadAllBytes(file);
                var fileName = Path.GetFileName(file);
                GenerateColorVariants(spriteData, outputPath, fileName);
            }

            return sprFiles.Length + binFiles.Length;
        }

        public void ProcessSingleSprite(string spriteFile, string outputPath)
        {
            // TLDR: Process a single sprite file and generate color variants
            var spriteData = File.ReadAllBytes(spriteFile);
            var fileName = Path.GetFileName(spriteFile);
            GenerateColorVariants(spriteData, outputPath, fileName);
        }
    }
}