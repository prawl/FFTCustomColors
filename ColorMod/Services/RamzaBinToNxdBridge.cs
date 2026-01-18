using System;
using System.Drawing;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Bridge service that converts BIN/SPR palette data to NXD CLUTData format.
    /// This allows editing Ramza's colors using the standard SPR-based Theme Editor
    /// and then transferring the results to the charclut.nxd database.
    /// </summary>
    public class RamzaBinToNxdBridge
    {
        /// <summary>
        /// Converts BGR555 palette bytes (from SPR file) to CLUTData format (48 integers).
        /// CLUTData is an array of 48 integers representing 16 RGB colors: [R0,G0,B0, R1,G1,B1, ...]
        /// </summary>
        /// <param name="paletteBytes">512 bytes (256 colors × 2 bytes per color in BGR555 format)</param>
        /// <returns>48 integers representing 16 colors in RGB format</returns>
        public int[] ConvertBinPaletteToClutData(byte[] paletteBytes)
        {
            if (paletteBytes == null || paletteBytes.Length < 32)
                throw new ArgumentException("Palette data must be at least 32 bytes (16 colors × 2 bytes)");

            var clutData = new int[48]; // 16 colors × 3 RGB values

            for (int colorIndex = 0; colorIndex < 16; colorIndex++)
            {
                int byteOffset = colorIndex * 2;
                var color = Bgr555ToColor(paletteBytes[byteOffset], paletteBytes[byteOffset + 1]);

                int clutOffset = colorIndex * 3;
                clutData[clutOffset] = color.R;
                clutData[clutOffset + 1] = color.G;
                clutData[clutOffset + 2] = color.B;
            }

            return clutData;
        }

        /// <summary>
        /// Converts CLUTData format (48 integers) to BGR555 palette bytes.
        /// </summary>
        /// <param name="clutData">48 integers representing 16 colors in RGB format</param>
        /// <returns>32 bytes (16 colors × 2 bytes per color in BGR555 format)</returns>
        public byte[] ConvertClutDataToBinPalette(int[] clutData)
        {
            if (clutData == null || clutData.Length < 48)
                throw new ArgumentException("CLUTData must be at least 48 integers (16 colors × 3 RGB values)");

            var paletteBytes = new byte[32]; // 16 colors × 2 bytes

            for (int colorIndex = 0; colorIndex < 16; colorIndex++)
            {
                int clutOffset = colorIndex * 3;
                var color = Color.FromArgb(
                    ClampByte(clutData[clutOffset]),
                    ClampByte(clutData[clutOffset + 1]),
                    ClampByte(clutData[clutOffset + 2]));

                int byteOffset = colorIndex * 2;
                var bgr555 = ColorToBgr555(color);
                paletteBytes[byteOffset] = bgr555.low;
                paletteBytes[byteOffset + 1] = bgr555.high;
            }

            return paletteBytes;
        }

        /// <summary>
        /// Gets the NXD Key value for a given Ramza chapter.
        /// </summary>
        public int GetNxdKeyForChapter(int chapter)
        {
            return chapter switch
            {
                1 => 1,  // Chapter 1
                2 => 2,  // Chapter 2/3
                23 => 2, // Alternate representation of Ch2/3
                4 => 3,  // Chapter 4
                _ => throw new ArgumentException($"Invalid chapter number: {chapter}. Valid values are 1, 2, 4.")
            };
        }

        /// <summary>
        /// Gets the chapter number from an NXD Key value.
        /// </summary>
        public int GetChapterFromNxdKey(int nxdKey)
        {
            return nxdKey switch
            {
                1 => 1,  // Chapter 1
                2 => 2,  // Chapter 2/3 (or could be 23)
                3 => 4,  // Chapter 4
                _ => throw new ArgumentException($"Invalid NXD key: {nxdKey}. Valid values are 1, 2, 3.")
            };
        }

        /// <summary>
        /// Gets the sprite filename for a given chapter.
        /// </summary>
        public string GetSpriteFilenameForChapter(int chapter)
        {
            return chapter switch
            {
                1 => "battle_ramuza_spr.bin",
                2 => "battle_ramuza2_spr.bin",
                23 => "battle_ramuza2_spr.bin",
                4 => "battle_ramuza3_spr.bin",
                _ => throw new ArgumentException($"Invalid chapter number: {chapter}")
            };
        }

        /// <summary>
        /// Gets the job name for a given chapter (matches section mapping files).
        /// </summary>
        public string GetJobNameForChapter(int chapter)
        {
            return chapter switch
            {
                1 => "RamzaCh1",
                2 => "RamzaCh23",
                23 => "RamzaCh23",
                4 => "RamzaCh4",
                _ => throw new ArgumentException($"Invalid chapter number: {chapter}")
            };
        }

        /// <summary>
        /// Converts BGR555 format (2 bytes) to a Color.
        /// BGR555: GGGBBBBB 0RRRRRGG (low byte, high byte)
        /// </summary>
        private Color Bgr555ToColor(byte low, byte high)
        {
            // BGR555: bits are laid out as 0BBBBBGG GGGRRRRR when read as little-endian 16-bit
            ushort value = (ushort)(low | (high << 8));

            int r = (value & 0x1F) * 255 / 31;
            int g = ((value >> 5) & 0x1F) * 255 / 31;
            int b = ((value >> 10) & 0x1F) * 255 / 31;

            return Color.FromArgb(r, g, b);
        }

        /// <summary>
        /// Converts a Color to BGR555 format (2 bytes).
        /// </summary>
        private (byte low, byte high) ColorToBgr555(Color color)
        {
            int r = color.R * 31 / 255;
            int g = color.G * 31 / 255;
            int b = color.B * 31 / 255;

            ushort value = (ushort)(r | (g << 5) | (b << 10));

            return ((byte)(value & 0xFF), (byte)((value >> 8) & 0xFF));
        }

        private static int ClampByte(int value)
        {
            if (value < 0) return 0;
            if (value > 255) return 255;
            return value;
        }
    }
}
