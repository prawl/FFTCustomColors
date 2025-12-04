using System.Collections.Generic;

namespace FFTColorMod;

public class MemoryScanner
{
    public List<int> ScanForPalettes(byte[] memory, PaletteDetector detector)
    {
        var foundAddresses = new List<int>();

        // Scan for both Chapter 1 and Chapter 2 palettes
        for (int i = 0; i <= memory.Length - 3; i++)
        {
            // Check for Chapter 1 palette (light blue)
            if (i + 2 < memory.Length &&
                memory[i] == 0xA0 &&
                memory[i + 1] == 0x60 &&
                memory[i + 2] == 0x40)
            {
                foundAddresses.Add(i);
            }
            // Check for Chapter 2 palette (purple-blue)
            else if (i + 2 < memory.Length &&
                memory[i] == 0x80 &&
                memory[i + 1] == 0x40 &&
                memory[i + 2] == 0x60)
            {
                foundAddresses.Add(i);
            }
        }

        return foundAddresses;
    }

    public void ApplyColorScheme(byte[] memory, int offset, string colorScheme, PaletteDetector detector, int chapter)
    {
        // Delegate to the existing PaletteDetector implementation
        detector.ReplacePaletteColors(memory, offset, colorScheme, chapter);
    }

    public List<(long memoryAddress, int chapter, int bufferOffset)> ScanForAllPalettesInMemoryRegions(
        List<(byte[] data, long baseOffset)> memoryRegions, PaletteDetector detector)
    {
        var allFoundPalettes = new List<(long memoryAddress, int chapter, int bufferOffset)>();

        foreach (var region in memoryRegions)
        {
            var foundOffsets = ScanForPalettes(region.data, detector);

            foreach (var offset in foundOffsets)
            {
                int chapter = detector.DetectChapterOutfit(region.data, offset);
                long memoryAddress = region.baseOffset + offset;

                allFoundPalettes.Add((memoryAddress, chapter, offset));
            }
        }

        return allFoundPalettes;
    }
}