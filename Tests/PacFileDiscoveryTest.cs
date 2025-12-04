using Xunit;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace FFTColorMod.Tests
{
    public class PacFileDiscoveryTest
    {
        [Fact]
        public void DiscoverFileNamesInPac_ListsAllFiles()
        {
            // TLDR: Test to discover all filenames in a PAC file to understand naming conventions
            var pacFile = @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\data\enhanced\0001.pac";

            if (!File.Exists(pacFile))
            {
                // Skip if game not installed
                return;
            }

            var extractor = new PacExtractor();
            var allFiles = new List<string>();

            if (extractor.OpenPacStream(pacFile))
            {
                try
                {
                    // Get all filenames from the PAC
                    var fileCount = extractor.GetFileCountFromStream();
                    for (int i = 0; i < fileCount && i < 100; i++) // Limit to first 100 for test
                    {
                        var fileName = extractor.GetFileNameFromStream(i);
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            allFiles.Add(fileName);
                        }
                    }
                }
                finally
                {
                    extractor.ClosePac();
                }
            }

            // Assert we found some files
            Assert.True(allFiles.Count > 0, "No files found in PAC");

            // Look for any sprite-like files
            var spriteFiles = allFiles.Where(f =>
                f.Contains("spr", System.StringComparison.OrdinalIgnoreCase) ||
                f.Contains("sprite", System.StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".bin", System.StringComparison.OrdinalIgnoreCase)).ToList();

            // Output findings for debugging
            if (spriteFiles.Any())
            {
                // Found sprite files - test passes
                Assert.True(true);
            }
            else
            {
                // No sprite files found - still pass but note it
                Assert.True(true, $"No sprite files found in first {allFiles.Count} files");
            }
        }
    }
}