using System;
using System.Drawing;
using System.IO;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.ThemeEditor
{
    public class PaletteModifier
    {
        private byte[] _originalData;
        private byte[] _workingData;
        private readonly BinSpriteExtractor _extractor = new BinSpriteExtractor();

        public bool IsLoaded { get; private set; }

        public void LoadTemplate(string binPath)
        {
            if (!File.Exists(binPath))
                throw new FileNotFoundException($"Template file not found: {binPath}");

            _originalData = File.ReadAllBytes(binPath);
            _workingData = (byte[])_originalData.Clone();
            IsLoaded = true;
        }

        public void SetPaletteColor(int index, Color color)
        {
            // Convert RGB to BGR555
            int r5 = (color.R * 31) / 255;
            int g5 = (color.G * 31) / 255;
            int b5 = (color.B * 31) / 255;
            ushort bgr555 = (ushort)(r5 | (g5 << 5) | (b5 << 10));

            // Write to palette 0 at specified index
            int offset = index * 2;
            _workingData[offset] = (byte)(bgr555 & 0xFF);
            _workingData[offset + 1] = (byte)(bgr555 >> 8);
        }

        /// <summary>
        /// Gets the color at the specified palette index.
        /// </summary>
        public Color GetPaletteColor(int index)
        {
            int offset = index * 2;
            ushort bgr555 = (ushort)(_workingData[offset] | (_workingData[offset + 1] << 8));

            // Convert BGR555 to RGB
            int r5 = bgr555 & 0x1F;
            int g5 = (bgr555 >> 5) & 0x1F;
            int b5 = (bgr555 >> 10) & 0x1F;

            int r = (r5 * 255) / 31;
            int g = (g5 * 255) / 31;
            int b = (b5 * 255) / 31;

            return Color.FromArgb(r, g, b);
        }

        public byte[] GetModifiedPalette()
        {
            var palette = new byte[512];
            Array.Copy(_workingData, 0, palette, 0, 512);
            return palette;
        }

        public void Reset()
        {
            _workingData = (byte[])_originalData.Clone();
        }

        /// <summary>
        /// Gets a preview bitmap for the specified compass direction.
        /// </summary>
        /// <param name="directionIndex">Compass direction: 0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW</param>
        /// <returns>The sprite bitmap for the specified direction</returns>
        public Bitmap GetPreview(int directionIndex = 5) // Default to SW
        {
            // ExtractAllDirections returns sprites indexed by compass direction:
            // 0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW
            var sprites = _extractor.ExtractAllDirections(_workingData, characterIndex: 0, paletteIndex: 0);
            return sprites[directionIndex];
        }

        public void ApplySectionColor(JobSection section, Color baseColor)
        {
            var shades = HslColor.GenerateShades(baseColor);

            for (int i = 0; i < section.Indices.Length; i++)
            {
                var role = section.Roles[i];

                // Skip accent roles - they should preserve original palette colors
                if (role == "accent" || role == "accent_shadow")
                    continue;

                var color = role switch
                {
                    "shadow" => shades.Shadow,
                    "highlight" => shades.Highlight,
                    _ => shades.Base // "base" or any other role
                };

                SetPaletteColor(section.Indices[i], color);
            }
        }

        public void SaveToFile(string outputPath)
        {
            File.WriteAllBytes(outputPath, _workingData);
        }
    }
}
