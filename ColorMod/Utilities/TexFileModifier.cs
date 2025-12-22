using System;
using System.IO;
using System.IO.Compression;

namespace FFTColorCustomizer.Utilities
{
    /// <summary>
    /// Modifies colors in tex files used by Ramza characters
    /// </summary>
    public class TexFileModifier
    {
        private const int StandardTexSize = 131072;
        private const int YoxHeaderOffset = 0x400;

        /// <summary>
        /// Checks if a tex file is YOX compressed
        /// </summary>
        public bool IsYoxCompressed(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                if (fs.Length <= YoxHeaderOffset + 4)
                    return false;

                fs.Seek(YoxHeaderOffset, SeekOrigin.Begin);
                byte[] header = new byte[4];
                fs.Read(header, 0, 4);

                // Check for "YOX\0" signature
                return header[0] == 'Y' && header[1] == 'O' &&
                       header[2] == 'X' && header[3] == 0;
            }
        }

        /// <summary>
        /// Decompresses a tex file if needed, otherwise returns raw data
        /// </summary>
        public byte[] DecompressTex(string filePath)
        {
            byte[] fileData = File.ReadAllBytes(filePath);

            if (IsYoxCompressed(filePath))
            {
                // Skip to compressed data (YOX header + 16 bytes)
                int dataStart = YoxHeaderOffset + 16;

                using (var compressedStream = new MemoryStream(fileData, dataStart, fileData.Length - dataStart))
                using (var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
                using (var resultStream = new MemoryStream())
                {
                    // Skip first two bytes (zlib header)
                    compressedStream.Seek(2, SeekOrigin.Begin);

                    deflateStream.CopyTo(resultStream);
                    return resultStream.ToArray();
                }
            }
            else
            {
                // Return uncompressed data
                return fileData;
            }
        }

        /// <summary>
        /// Transforms a color based on the theme
        /// </summary>
        public (int r, int g, int b) TransformColor(int r, int g, int b, string theme)
        {
            if (theme == "white_heretic")
            {
                // Brown armor colors -> White/light gray
                if (IsBrownArmorColor(r, g, b))
                {
                    if (r > 60)
                        return (224, 248, 248); // Bright white
                    else
                        return (216, 232, 208); // Light gray
                }

                // Purple armor -> light gray
                if (IsPurpleArmorColor(r, g, b))
                {
                    return (216, 232, 208);
                }

                // Keep skin tones unchanged
                if (IsSkinTone(r, g, b))
                {
                    return (r, g, b);
                }
            }

            // Default: no change
            return (r, g, b);
        }

        /// <summary>
        /// Converts RGB888 to RGB555 format
        /// </summary>
        public ushort RgbToRgb555(int r, int g, int b)
        {
            return (ushort)(((r >> 3) & 0x1F) |
                           (((g >> 3) & 0x1F) << 5) |
                           (((b >> 3) & 0x1F) << 10));
        }

        /// <summary>
        /// Converts RGB555 to RGB888 format
        /// </summary>
        public (int r, int g, int b) Rgb555ToRgb(ushort rgb555)
        {
            int r = (rgb555 & 0x1F) << 3;
            int g = ((rgb555 >> 5) & 0x1F) << 3;
            int b = ((rgb555 >> 10) & 0x1F) << 3;
            return (r, g, b);
        }

        /// <summary>
        /// Modifies colors in a tex file and returns the number of changes
        /// </summary>
        public int ModifyTexColors(string inputPath, string outputPath, string theme)
        {
            byte[] data = DecompressTex(inputPath);
            bool wasCompressed = IsYoxCompressed(inputPath);
            int changesCount = 0;

            // Process the data
            byte[] modifiedData = new byte[StandardTexSize];
            Array.Copy(data, modifiedData, Math.Min(data.Length, StandardTexSize));

            // Process as 16-bit RGB555 colors
            int dataLength = wasCompressed ? Math.Min(10000, data.Length) : data.Length;

            for (int offset = 0; offset < dataLength - 1; offset += 2)
            {
                // Read 16-bit color
                ushort value = (ushort)(data[offset] | (data[offset + 1] << 8));

                if (value != 0) // Skip black
                {
                    var (r, g, b) = Rgb555ToRgb(value);
                    var (newR, newG, newB) = TransformColor(r, g, b, theme);

                    if (r != newR || g != newG || b != newB)
                    {
                        ushort newValue = RgbToRgb555(newR, newG, newB);
                        modifiedData[offset] = (byte)(newValue & 0xFF);
                        modifiedData[offset + 1] = (byte)((newValue >> 8) & 0xFF);
                        changesCount++;
                    }
                }
            }

            // Save the modified file
            if (wasCompressed)
            {
                SaveCompressedTex(inputPath, modifiedData, outputPath, dataLength);
            }
            else
            {
                File.WriteAllBytes(outputPath, modifiedData);
            }

            return changesCount;
        }

        private void SaveCompressedTex(string originalPath, byte[] modifiedData, string outputPath, int dataLength)
        {
            byte[] original = File.ReadAllBytes(originalPath);
            byte[] result = new byte[StandardTexSize];

            // Copy original header
            Array.Copy(original, 0, result, 0, YoxHeaderOffset);

            // Copy YOX header
            Array.Copy(original, YoxHeaderOffset, result, YoxHeaderOffset, 16);

            // Compress the modified data
            using (var outputStream = new MemoryStream())
            {
                // Write zlib header
                outputStream.WriteByte(0x78);
                outputStream.WriteByte(0x9C);

                using (var deflateStream = new DeflateStream(outputStream, CompressionLevel.Optimal, true))
                {
                    deflateStream.Write(modifiedData, 0, dataLength);
                }

                // Copy compressed data to result
                byte[] compressed = outputStream.ToArray();
                Array.Copy(compressed, 0, result, YoxHeaderOffset + 16, compressed.Length);
            }

            File.WriteAllBytes(outputPath, result);
        }

        private bool IsBrownArmorColor(int r, int g, int b)
        {
            return (r >= 40 && r <= 120) &&
                   (g >= 20 && g <= 80) &&
                   (b >= 10 && b <= 80) &&
                   r > b;
        }

        private bool IsPurpleArmorColor(int r, int g, int b)
        {
            return (r >= 40 && r <= 100) &&
                   (g >= 30 && g <= 70) &&
                   (b >= 40 && b <= 100) &&
                   Math.Abs(r - b) < 30;
        }

        private bool IsSkinTone(int r, int g, int b)
        {
            return (r >= 140 && r <= 240) &&
                   (g >= 100 && g <= 200) &&
                   (b >= 60 && b <= 160) &&
                   r > g && g > b;
        }
    }
}