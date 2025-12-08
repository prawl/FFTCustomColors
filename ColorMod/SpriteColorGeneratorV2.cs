using System.IO;

namespace FFTColorMod
{
    public class SpriteColorGeneratorV2
    {
        // Improved sprite color generator that works with any FFT sprite
        private readonly ImprovedPaletteHandler _paletteHandler;

        public SpriteColorGeneratorV2()
        {
            _paletteHandler = new ImprovedPaletteHandler();
        }

        public void GenerateColorVariants(byte[] spriteData, string outputPath, string fileName)
        {
            // Create output directories for each color variant
            Directory.CreateDirectory(Path.Combine(outputPath, "sprites_blue"));
            Directory.CreateDirectory(Path.Combine(outputPath, "sprites_red"));
            Directory.CreateDirectory(Path.Combine(outputPath, "sprites_green"));
            Directory.CreateDirectory(Path.Combine(outputPath, "sprites_purple"));
            Directory.CreateDirectory(Path.Combine(outputPath, "sprites_original"));

            // Write original unchanged
            File.WriteAllBytes(Path.Combine(outputPath, "sprites_original", fileName), spriteData);

            // Find palette start (typically at beginning for FFT sprites)
            int paletteOffset = _paletteHandler.FindPaletteStart(spriteData);

            if (paletteOffset >= 0)
            {
                // Generate color variants using generic palette handler
                var blueData = (byte[])spriteData.Clone();
                _paletteHandler.ApplyColorTransform(blueData, paletteOffset, "blue");
                File.WriteAllBytes(Path.Combine(outputPath, "sprites_blue", fileName), blueData);

                var redData = (byte[])spriteData.Clone();
                _paletteHandler.ApplyColorTransform(redData, paletteOffset, "red");
                File.WriteAllBytes(Path.Combine(outputPath, "sprites_red", fileName), redData);

                var greenData = (byte[])spriteData.Clone();
                _paletteHandler.ApplyColorTransform(greenData, paletteOffset, "green");
                File.WriteAllBytes(Path.Combine(outputPath, "sprites_green", fileName), greenData);

                var purpleData = (byte[])spriteData.Clone();
                _paletteHandler.ApplyColorTransform(purpleData, paletteOffset, "purple");
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
            // Process all sprite files (.spr and _spr.bin) in directory
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
            // Process a single sprite file and generate color variants
            var spriteData = File.ReadAllBytes(spriteFile);
            var fileName = Path.GetFileName(spriteFile);
            GenerateColorVariants(spriteData, outputPath, fileName);
        }
    }
}