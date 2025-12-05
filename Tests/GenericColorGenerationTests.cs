using System;
using System.IO;
using Xunit;

namespace FFTColorMod.Tests
{
    public class GenericColorGenerationTests
    {
        [Fact]
        public void TestGenericPaletteHandler_GeneratesColorVariants()
        {
            // Arrange - Create a simple test sprite with palette data
            var testSprite = new byte[100];

            // Fill with some test palette data (not pure black)
            for (int i = 0; i < 48; i += 3)
            {
                testSprite[i] = 50;     // B
                testSprite[i + 1] = 100; // G
                testSprite[i + 2] = 150; // R
            }

            var handler = new GenericPaletteHandler();

            // Act - Apply red transformation
            var redSprite = (byte[])testSprite.Clone();
            handler.ApplyColorTransform(redSprite, 0, "red");

            // Assert - Colors should be different
            bool isDifferent = false;
            for (int i = 0; i < 48; i++)
            {
                if (testSprite[i] != redSprite[i])
                {
                    isDifferent = true;
                    break;
                }
            }

            Assert.True(isDifferent, "Red transformation should change colors");

            // Additional assertions - red channel should be boosted
            Assert.True(redSprite[2] > testSprite[2], "Red channel should be increased");
            Assert.True(redSprite[0] < testSprite[0], "Blue channel should be decreased");
        }

        [Fact]
        public void TestSpriteColorGeneratorV2_CreatesAllColorVariants()
        {
            // Arrange
            var testDir = Path.Combine(Path.GetTempPath(), "fft_color_test_" + Guid.NewGuid());
            Directory.CreateDirectory(testDir);

            try
            {
                var inputDir = Path.Combine(testDir, "input");
                var outputDir = Path.Combine(testDir, "output");
                Directory.CreateDirectory(inputDir);

                // Create a test sprite file
                var testSprite = new byte[200];
                for (int i = 0; i < 96; i += 3)
                {
                    testSprite[i] = (byte)(30 + i);     // B
                    testSprite[i + 1] = (byte)(60 + i); // G
                    testSprite[i + 2] = (byte)(90 + i); // R
                }

                var testFile = Path.Combine(inputDir, "test_spr.bin");
                File.WriteAllBytes(testFile, testSprite);

                var generator = new SpriteColorGeneratorV2();

                // Act
                generator.ProcessSingleSprite(testFile, outputDir);

                // Assert - All variant directories should exist
                Assert.True(Directory.Exists(Path.Combine(outputDir, "sprites_original")));
                Assert.True(Directory.Exists(Path.Combine(outputDir, "sprites_red")));
                Assert.True(Directory.Exists(Path.Combine(outputDir, "sprites_blue")));
                Assert.True(Directory.Exists(Path.Combine(outputDir, "sprites_green")));
                Assert.True(Directory.Exists(Path.Combine(outputDir, "sprites_purple")));

                // Assert - Files should be different
                var original = File.ReadAllBytes(Path.Combine(outputDir, "sprites_original", "test_spr.bin"));
                var red = File.ReadAllBytes(Path.Combine(outputDir, "sprites_red", "test_spr.bin"));
                var blue = File.ReadAllBytes(Path.Combine(outputDir, "sprites_blue", "test_spr.bin"));

                bool redDifferent = false, blueDifferent = false;
                for (int i = 0; i < Math.Min(96, original.Length); i++)
                {
                    if (original[i] != red[i]) redDifferent = true;
                    if (original[i] != blue[i]) blueDifferent = true;
                }

                Assert.True(redDifferent, "Red variant should be different from original");
                Assert.True(blueDifferent, "Blue variant should be different from original");
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                }
            }
        }
    }
}