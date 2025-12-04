using Xunit;
using System.IO;

namespace FFTColorMod.Tests
{
    public class IntegrationTests
    {
        [Fact]
        public void FindRamzaInActualGameFiles()
        {
            // TLDR: Test finding Ramza sprites in actual FFT game files (skip if game not installed)
            var gameDir = @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS\data\enhanced";

            if (!Directory.Exists(gameDir))
            {
                // Skip test if game not installed
                return;
            }

            var outputDir = Path.Combine(Path.GetTempPath(), "ramza_sprites_test");

            try
            {
                var extractor = new PacExtractor();
                var extracted = extractor.FindAndExtractSpritesUsingStream(gameDir, "ramza", outputDir);

                // We should find at least one Ramza sprite
                Assert.True(extracted.Count > 0, "No Ramza sprites found in game files");

                // Verify files were actually extracted
                foreach (var file in extracted)
                {
                    var fullPath = Path.Combine(outputDir, file);
                    Assert.True(File.Exists(fullPath), $"Extracted file not found: {file}");
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