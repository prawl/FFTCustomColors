using System;
using System.IO;
using System.Linq;
using Xunit;

namespace FFTColorMod.Tests
{
    public class SpriteColorGeneratorTests
    {
        [Fact]
        public void Constructor_CreatesInstance()
        {
            // TLDR: SpriteColorGenerator can be instantiated
            var generator = new SpriteColorGenerator();
            Assert.NotNull(generator);
        }

        [Fact]
        public void GenerateColorVariants_WithEmptySpriteData_CreatesOutputDirectories()
        {
            // TLDR: Creates 5 output directories for each color variant
            var tempPath = Path.Combine(Path.GetTempPath(), "FFTColorModTest_" + Guid.NewGuid());
            Directory.CreateDirectory(tempPath);

            try
            {
                var generator = new SpriteColorGenerator();
                generator.GenerateColorVariants(new byte[0], tempPath, "test.spr");

                Assert.True(Directory.Exists(Path.Combine(tempPath, "sprites_blue")));
                Assert.True(Directory.Exists(Path.Combine(tempPath, "sprites_red")));
                Assert.True(Directory.Exists(Path.Combine(tempPath, "sprites_green")));
                Assert.True(Directory.Exists(Path.Combine(tempPath, "sprites_purple")));
                Assert.True(Directory.Exists(Path.Combine(tempPath, "sprites_original")));
            }
            finally
            {
                if (Directory.Exists(tempPath))
                    Directory.Delete(tempPath, true);
            }
        }

        [Fact]
        public void GenerateColorVariants_WithRamzaPalette_CreatesColoredFiles()
        {
            // TLDR: Creates actual color variant files with modified palettes
            var tempPath = Path.Combine(Path.GetTempPath(), "FFTColorModTest_" + Guid.NewGuid());
            Directory.CreateDirectory(tempPath);

            try
            {
                // Create sprite data with Ramza's brown colors
                var spriteData = new byte[1024];
                // Add Ramza brown palette at offset 100
                spriteData[100] = 0x17; // B
                spriteData[101] = 0x2C; // G
                spriteData[102] = 0x4A; // R

                var generator = new SpriteColorGenerator();
                generator.GenerateColorVariants(spriteData, tempPath, "ramza.spr");

                // Check that files were created in each directory
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_blue", "ramza.spr")));
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_red", "ramza.spr")));
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_green", "ramza.spr")));
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_purple", "ramza.spr")));
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_original", "ramza.spr")));

                // Verify blue variant has modified colors
                var blueData = File.ReadAllBytes(Path.Combine(tempPath, "sprites_blue", "ramza.spr"));
                Assert.Equal(1024, blueData.Length);
                // Blue variant should have blue-shifted colors
                Assert.NotEqual(spriteData[100], blueData[100]); // Color should be changed
            }
            finally
            {
                if (Directory.Exists(tempPath))
                    Directory.Delete(tempPath, true);
            }
        }

        [Fact]
        public void GenerateColorVariants_PreservesOriginalFile()
        {
            // TLDR: Original sprite file remains completely unchanged
            var tempPath = Path.Combine(Path.GetTempPath(), "FFTColorModTest_" + Guid.NewGuid());
            Directory.CreateDirectory(tempPath);

            try
            {
                var spriteData = new byte[512];
                // Fill with test pattern
                for (int i = 0; i < spriteData.Length; i++)
                {
                    spriteData[i] = (byte)(i % 256);
                }

                var generator = new SpriteColorGenerator();
                generator.GenerateColorVariants(spriteData, tempPath, "test.spr");

                var originalData = File.ReadAllBytes(Path.Combine(tempPath, "sprites_original", "test.spr"));
                Assert.Equal(spriteData, originalData); // Original should be exact copy
            }
            finally
            {
                if (Directory.Exists(tempPath))
                    Directory.Delete(tempPath, true);
            }
        }

        [Fact]
        public void GenerateColorVariants_HandlesSmallFiles()
        {
            // TLDR: Handles files smaller than typical palette offset
            var tempPath = Path.Combine(Path.GetTempPath(), "FFTColorModTest_" + Guid.NewGuid());
            Directory.CreateDirectory(tempPath);

            try
            {
                var spriteData = new byte[50]; // Smaller than offset 100
                var generator = new SpriteColorGenerator();

                // Should not throw
                generator.GenerateColorVariants(spriteData, tempPath, "small.spr");

                // All files should exist
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_blue", "small.spr")));
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_original", "small.spr")));
            }
            finally
            {
                if (Directory.Exists(tempPath))
                    Directory.Delete(tempPath, true);
            }
        }

        [Fact]
        public void ProcessDirectory_HandlesMultipleFiles()
        {
            // TLDR: Batch processes all sprites in a directory
            var tempPath = Path.Combine(Path.GetTempPath(), "FFTColorModTest_" + Guid.NewGuid());
            var inputPath = Path.Combine(tempPath, "input");
            Directory.CreateDirectory(inputPath);

            try
            {
                // Create multiple test files
                File.WriteAllBytes(Path.Combine(inputPath, "sprite1.spr"), new byte[256]);
                File.WriteAllBytes(Path.Combine(inputPath, "sprite2.spr"), new byte[512]);
                File.WriteAllBytes(Path.Combine(inputPath, "sprite3.spr"), new byte[1024]);

                var generator = new SpriteColorGenerator();
                generator.ProcessDirectory(inputPath, tempPath);

                // Check all files were processed
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_blue", "sprite1.spr")));
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_blue", "sprite2.spr")));
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_blue", "sprite3.spr")));
            }
            finally
            {
                if (Directory.Exists(tempPath))
                    Directory.Delete(tempPath, true);
            }
        }

        [Fact]
        public void ProcessSingleSprite_CreatesColorVariantsForOneFile()
        {
            // TLDR: Processes a single sprite file and creates all color variants
            var tempPath = Path.Combine(Path.GetTempPath(), "FFTColorModTest_" + Guid.NewGuid());
            var inputPath = Path.Combine(tempPath, "input");
            Directory.CreateDirectory(inputPath);

            try
            {
                // Create a test sprite file
                var spriteFile = Path.Combine(inputPath, "test.spr");
                var spriteData = new byte[256];
                File.WriteAllBytes(spriteFile, spriteData);

                var generator = new SpriteColorGenerator();
                generator.ProcessSingleSprite(spriteFile, tempPath);

                // Check all color variant files were created
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_blue", "test.spr")));
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_red", "test.spr")));
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_green", "test.spr")));
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_purple", "test.spr")));
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_original", "test.spr")));
            }
            finally
            {
                if (Directory.Exists(tempPath))
                    Directory.Delete(tempPath, true);
            }
        }

        [Fact]
        public void ProcessDirectory_ProcessesBinFiles()
        {
            // TLDR: Processes .bin sprite files (FFT format) in addition to .spr files
            var tempPath = Path.Combine(Path.GetTempPath(), "FFTColorModTest_" + Guid.NewGuid());
            var inputPath = Path.Combine(tempPath, "input");
            Directory.CreateDirectory(inputPath);

            try
            {
                // Create test .bin file with FFT sprite naming convention
                File.WriteAllBytes(Path.Combine(inputPath, "battle_ramza_spr.bin"), new byte[256]);

                var generator = new SpriteColorGenerator();
                generator.ProcessDirectory(inputPath, tempPath);

                // Check the .bin file was processed
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_blue", "battle_ramza_spr.bin")));
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_red", "battle_ramza_spr.bin")));
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_green", "battle_ramza_spr.bin")));
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_purple", "battle_ramza_spr.bin")));
                Assert.True(File.Exists(Path.Combine(tempPath, "sprites_original", "battle_ramza_spr.bin")));
            }
            finally
            {
                if (Directory.Exists(tempPath))
                    Directory.Delete(tempPath, true);
            }
        }
    }
}