using System;
using System.IO;
using Xunit;

namespace FFTColorMod.Tests
{
    public class SpriteProcessingPipelineTests
    {
        [Fact]
        public void ProcessSprite_WithRamzaColors_GeneratesColorVariants()
        {
            // TLDR: Full pipeline test - detect palette and generate variants
            var tempPath = Path.Combine(Path.GetTempPath(), "FFTColorModTest_" + Guid.NewGuid());
            Directory.CreateDirectory(tempPath);

            try
            {
                // Create mock sprite data with Ramza's brown colors
                var spriteData = CreateMockRamzaSprite();

                // Process through pipeline
                var pipeline = new SpriteProcessingPipeline();
                pipeline.ProcessSprite(spriteData, tempPath, "ramza.spr");

                // Verify all variants were created
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_blue", "ramza.spr")));
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_red", "ramza.spr")));
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_green", "ramza.spr")));
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_purple", "ramza.spr")));
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_original", "ramza.spr")));

                // Verify colors were actually changed in blue variant
                var blueVariant = File.ReadAllBytes(Path.Combine(tempPath, "sprites_blue", "ramza.spr"));
                var originalData = File.ReadAllBytes(Path.Combine(tempPath, "sprites_original", "ramza.spr"));

                // Find where the brown colors were and verify they changed
                bool foundColorChange = false;
                for (int i = 0; i < blueVariant.Length - 2; i++)
                {
                    if (originalData[i] == 0x17 && originalData[i+1] == 0x2C && originalData[i+2] == 0x4A)
                    {
                        // Found Ramza brown - verify it changed in blue variant
                        if (blueVariant[i] != originalData[i] ||
                            blueVariant[i+1] != originalData[i+1] ||
                            blueVariant[i+2] != originalData[i+2])
                        {
                            foundColorChange = true;
                            break;
                        }
                    }
                }
                Assert.True(foundColorChange, "Blue variant should have different colors than original");
            }
            finally
            {
                if (Directory.Exists(tempPath))
                    Directory.Delete(tempPath, true);
            }
        }

        private byte[] CreateMockRamzaSprite()
        {
            // TLDR: Create sprite data with known Ramza palette colors
            var data = new byte[2048];

            // Add Ramza's brown colors at various offsets (BGR format)
            // Main tunic brown
            data[100] = 0x17; data[101] = 0x2C; data[102] = 0x4A;
            // Darker brown
            data[200] = 0x0B; data[201] = 0x17; data[202] = 0x2C;
            // Lighter brown
            data[300] = 0x2C; data[301] = 0x4A; data[302] = 0x6D;
            // Another shade
            data[400] = 0x40; data[401] = 0x5F; data[402] = 0x8F;

            return data;
        }
    }
}