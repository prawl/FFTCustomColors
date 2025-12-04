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
        // Look for the first known brown color (0x17, 0x2C, 0x4A in BGR)
        for (int i = 0; i <= memoryData.Length - 3; i++)
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

        for (int i = 0; i <= memoryData.Length - 3; i++)
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

    public int DetectChapterOutfit(byte[] memoryData, int offset, bool preferChapter4 = false)
    {
        // Check for Chapter 1 light blue tunic
        if (offset + 2 < memoryData.Length &&
            memoryData[offset] == 0xA0 &&
            memoryData[offset + 1] == 0x60 &&
            memoryData[offset + 2] == 0x40)
        {
            return 1;
        }

        // Check for Chapter 2/4 purple-blue tunic (both use same colors 80 40 60)
        if (offset + 2 < memoryData.Length &&
            memoryData[offset] == 0x80 &&
            memoryData[offset + 1] == 0x40 &&
            memoryData[offset + 2] == 0x60)
        {
            // Chapter 4 uses same colors as Chapter 2, distinguish by context
            return preferChapter4 ? 4 : 2;
        }

        // Check for Chapter 3 burgundy outfit
        if (offset + 2 < memoryData.Length &&
            memoryData[offset] == 0x40 &&
            memoryData[offset + 1] == 0x30 &&
            memoryData[offset + 2] == 0x60)
        {
            return 3;
        }

        // For unrecognized patterns, default to Chapter 4
        return 4;
    }

    public void ReplacePaletteColors(byte[] memoryData, int offset, string colorScheme, int chapter = 0)
    {
        if (colorScheme.ToLower() == "red")
        {
            if (chapter == 1)
            {
                // Replace Chapter 1 blue colors with red
                if (offset + 2 < memoryData.Length &&
                    memoryData[offset] == 0xA0 &&
                    memoryData[offset + 1] == 0x60 &&
                    memoryData[offset + 2] == 0x40)
                {
                    memoryData[offset] = 0x40;     // B: reduced for red
                    memoryData[offset + 1] = 0x40; // G: reduced for red
                    memoryData[offset + 2] = 0xA0; // R: enhanced for red
                }

                if (offset + 5 < memoryData.Length &&
                    memoryData[offset + 3] == 0x80 &&
                    memoryData[offset + 4] == 0x50 &&
                    memoryData[offset + 5] == 0x30)
                {
                    memoryData[offset + 3] = 0x30; // B: reduced for red
                    memoryData[offset + 4] = 0x30; // G: reduced for red
                    memoryData[offset + 5] = 0x80; // R: enhanced for red
                }
            }
            else if (chapter == 2 || chapter == 4)
            {
                // Replace Chapter 2/4 purple colors with red (both chapters use same colors)
                if (offset + 2 < memoryData.Length &&
                    memoryData[offset] == 0x80 &&
                    memoryData[offset + 1] == 0x40 &&
                    memoryData[offset + 2] == 0x60)
                {
                    memoryData[offset] = 0x30;     // B: reduced for red
                    memoryData[offset + 1] = 0x30; // G: reduced for red
                    memoryData[offset + 2] = 0x80; // R: enhanced for red
                }

                if (offset + 5 < memoryData.Length &&
                    memoryData[offset + 3] == 0x60 &&
                    memoryData[offset + 4] == 0x30 &&
                    memoryData[offset + 5] == 0x50)
                {
                    memoryData[offset + 3] = 0x25; // B: reduced for red
                    memoryData[offset + 4] = 0x25; // G: reduced for red
                    memoryData[offset + 5] = 0x70; // R: enhanced for red
                }
            }
            else if (chapter == 3)
            {
                // Replace Chapter 3 burgundy colors with red
                if (offset + 2 < memoryData.Length &&
                    memoryData[offset] == 0x40 &&
                    memoryData[offset + 1] == 0x30 &&
                    memoryData[offset + 2] == 0x60)
                {
                    memoryData[offset] = 0x1A;     // B: reduced for red
                    memoryData[offset + 1] = 0x2C; // G: adjusted for red
                    memoryData[offset + 2] = 0x7F; // R: enhanced for red
                }

                if (offset + 5 < memoryData.Length &&
                    memoryData[offset + 3] == 0x30 &&
                    memoryData[offset + 4] == 0x25 &&
                    memoryData[offset + 5] == 0x50)
                {
                    memoryData[offset + 3] = 0x15; // B: reduced for red
                    memoryData[offset + 4] = 0x20; // G: adjusted for red
                    memoryData[offset + 5] = 0x65; // R: enhanced for red
                }
            }
            else
            {
                // Original brown color replacement (for backward compatibility)
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
        }
    }
}