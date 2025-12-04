using Xunit;
using System.IO;

namespace FFTColorMod.Tests
{
    public class SpriteProcessingIntegrationTest
    {
        [Fact]
        public void ProcessBattleDamiSprite_CreatesAllColorVariants()
        {
            // TLDR: Test processing the existing battle_dami sprite to verify all color variants are created
            var inputSprite = @"C:\Users\ptyRa\Dev\FFT_Color_Mod\FFTIVC\data\enhanced\fftpack\unit\sprites_original\battle_dami_spr.bin";

            if (!File.Exists(inputSprite))
            {
                // Skip test if sprite doesn't exist
                return;
            }

            var outputDir = Path.Combine(Path.GetTempPath(), "sprite_processing_test");

            try
            {
                // Process the sprite
                var generator = new SpriteColorGenerator();
                generator.ProcessSingleSprite(inputSprite, outputDir);

                // Verify all color variants were created
                var colors = new[] { "blue", "red", "green", "purple", "original" };
                foreach (var color in colors)
                {
                    var colorDir = Path.Combine(outputDir, $"sprites_{color}");
                    Assert.True(Directory.Exists(colorDir), $"Color directory not created: {color}");

                    var outputFile = Path.Combine(colorDir, "battle_dami_spr.bin");
                    Assert.True(File.Exists(outputFile), $"Color variant not created: {color}");

                    // Verify file has content
                    var fileInfo = new FileInfo(outputFile);
                    Assert.True(fileInfo.Length > 0, $"Color variant is empty: {color}");
                }
            }
            finally
            {
                if (Directory.Exists(outputDir))
                    Directory.Delete(outputDir, true);
            }
        }
    }
}