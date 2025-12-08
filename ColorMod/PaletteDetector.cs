using System.Collections.Generic;

namespace FFTColorMod;

public class PaletteDetector
{
    public bool IsBrownColor(byte r, byte g, byte b)
    {
        // Browns typically have R > G > B and low blue values
        return r > g && g > b && b < 50;
    }

    public int FindPalette(byte[] memoryData)
    {
        // TLDR: Palette data is only in first 288 bytes (96 colors × 3 bytes BGR)
        int searchLimit = System.Math.Min(288, memoryData.Length);

        // Look for the first known brown color (0x17, 0x2C, 0x4A in BGR)
        for (int i = 0; i <= searchLimit - 3; i++)
        {
            if (memoryData[i] == 0x17 &&
                memoryData[i + 1] == 0x2C &&
                memoryData[i + 2] == 0x4A)
            {
                return i;
            }
        }
        return -1;
    }

    public List<int> FindAllPalettes(byte[] memoryData)
    {
        var offsets = new List<int>();

        // TLDR: Palette data is only in first 288 bytes
        int searchLimit = System.Math.Min(288, memoryData.Length);

        for (int i = 0; i <= searchLimit - 3; i++)
        {
            if (memoryData[i] == 0x17 &&
                memoryData[i + 1] == 0x2C &&
                memoryData[i + 2] == 0x4A)
            {
                offsets.Add(i);
            }
        }

        return offsets;
    }

    public void ReplacePaletteColors(byte[] memoryData, int offset, string colorScheme)
    {
        if (colorScheme.ToLower() == "red")
        {
            // TLDR: Replace original brown with red
            if (offset + 2 < memoryData.Length &&
                memoryData[offset] == 0x17 &&
                memoryData[offset + 1] == 0x2C &&
                memoryData[offset + 2] == 0x4A)
            {
                memoryData[offset] = 0x1A;     // B: dark red blue component
                memoryData[offset + 1] = 0x2C; // G: keep similar
                memoryData[offset + 2] = 0x7F; // R: red (127)
            }

            if (offset + 5 < memoryData.Length &&
                memoryData[offset + 3] == 0x21 &&
                memoryData[offset + 4] == 0x3A &&
                memoryData[offset + 5] == 0x5A)
            {
                memoryData[offset + 3] = 0x2A; // B: medium red blue component
                memoryData[offset + 4] = 0x3A; // G: keep similar
                memoryData[offset + 5] = 0x9F; // R: brighter red (159)
            }
        }
        else if (colorScheme.ToLower() == "blue")
        {
            // TLDR: Replace original brown with blue
            if (offset + 2 < memoryData.Length &&
                memoryData[offset] == 0x17 &&
                memoryData[offset + 1] == 0x2C &&
                memoryData[offset + 2] == 0x4A)
            {
                memoryData[offset] = 0x7F;     // B: enhanced blue
                memoryData[offset + 1] = 0x2C; // G: keep similar
                memoryData[offset + 2] = 0x1A; // R: reduced
            }
        }
        else if (colorScheme.ToLower() == "green")
        {
            // TLDR: Replace original brown with green
            if (offset + 2 < memoryData.Length &&
                memoryData[offset] == 0x17 &&
                memoryData[offset + 1] == 0x2C &&
                memoryData[offset + 2] == 0x4A)
            {
                memoryData[offset] = 0x1A;     // B: reduced
                memoryData[offset + 1] = 0x7F; // G: enhanced green
                memoryData[offset + 2] = 0x2C; // R: keep similar
            }
        }
        else if (colorScheme.ToLower() == "purple")
        {
            // TLDR: Replace original brown with purple
            if (offset + 2 < memoryData.Length &&
                memoryData[offset] == 0x17 &&
                memoryData[offset + 1] == 0x2C &&
                memoryData[offset + 2] == 0x4A)
            {
                memoryData[offset] = 0x7F;     // B: enhanced for purple
                memoryData[offset + 1] = 0x2C; // G: keep similar
                memoryData[offset + 2] = 0x7F; // R: enhanced for purple
            }
        }
    }
}