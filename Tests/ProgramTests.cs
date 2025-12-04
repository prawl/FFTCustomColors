using Xunit;
using System.IO;

namespace FFTColorMod.Tests
{
    public class ProgramTests
    {
        [Fact]
        public void Main_FindRamza_ExtractsRamzaSprites()
        {
            // TLDR: Test that find-ramza command extracts Ramza sprites
            var tempDir = Path.Combine(Path.GetTempPath(), "program_find_ramza_test");
            var gameDir = Path.Combine(tempDir, "game");
            var outputDir = Path.Combine(tempDir, "output");

            try
            {
                Directory.CreateDirectory(gameDir);

                // Create test PAC with Ramza sprite
                var pacFile = Path.Combine(gameDir, "test.pac");
                var pacData = new System.Collections.Generic.List<byte>();
                pacData.AddRange(System.Text.Encoding.ASCII.GetBytes("PACK"));
                pacData.AddRange(System.BitConverter.GetBytes(1));

                // File entry: ramza_spr.bin at offset 48
                pacData.AddRange(System.BitConverter.GetBytes(48));
                pacData.AddRange(System.BitConverter.GetBytes(5));
                pacData.AddRange(System.Text.Encoding.ASCII.GetBytes("ramza_spr.bin".PadRight(32, '\0')));

                // File data
                pacData.AddRange(new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55 });

                File.WriteAllBytes(pacFile, pacData.ToArray());

                // Run find-ramza command
                var args = new[] { "find-ramza", gameDir, outputDir };
                Program.Main(args);

                // Verify Ramza sprite was extracted
                Assert.True(Directory.Exists(outputDir));
                var extractedFile = Path.Combine(outputDir, "ramza_spr.bin");
                Assert.True(File.Exists(extractedFile));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}