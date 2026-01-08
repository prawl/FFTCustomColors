using System;
using System.Collections.Generic;
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
        private readonly Dictionary<string, RelativeShadeGenerator> _shadeGenerators = new Dictionary<string, RelativeShadeGenerator>();

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
        /// Gets the color at the specified palette index from the working data.
        /// </summary>
        public Color GetPaletteColor(int index)
        {
            return GetColorFromData(_workingData, index);
        }

        /// <summary>
        /// Gets the original color at the specified palette index (before any modifications).
        /// </summary>
        public Color GetOriginalPaletteColor(int index)
        {
            return GetColorFromData(_originalData, index);
        }

        private Color GetColorFromData(byte[] data, int index)
        {
            int offset = index * 2;
            ushort bgr555 = (ushort)(data[offset] | (data[offset + 1] << 8));

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
            _shadeGenerators.Clear();
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
            // Get or create the shade generator for this section
            var generator = GetOrCreateShadeGenerator(section);

            // Apply colors using the relative shade generator
            foreach (var index in section.Indices)
            {
                var color = generator.GenerateShade(index, baseColor);
                SetPaletteColor(index, color);
            }
        }

        /// <summary>
        /// Gets or creates a RelativeShadeGenerator for the section.
        /// The generator captures the original color relationships from the sprite.
        /// </summary>
        private RelativeShadeGenerator GetOrCreateShadeGenerator(JobSection section)
        {
            if (_shadeGenerators.TryGetValue(section.Name, out var existing))
                return existing;

            // Build dictionary of original colors for this section
            var originalColors = new Dictionary<int, Color>();
            foreach (var index in section.Indices)
            {
                originalColors[index] = GetOriginalPaletteColor(index);
            }

            // Determine the primary index
            int primaryIndex = GetPrimaryIndex(section);

            var generator = new RelativeShadeGenerator(originalColors, primaryIndex);
            _shadeGenerators[section.Name] = generator;
            return generator;
        }

        /// <summary>
        /// Gets the primary index for a section (used as the base for color relationships).
        /// </summary>
        private int GetPrimaryIndex(JobSection section)
        {
            // If primaryIndex is explicitly set, use it
            if (section.PrimaryIndex.HasValue)
                return section.PrimaryIndex.Value;

            // Otherwise, find the index with "base" role
            for (int i = 0; i < section.Roles.Length; i++)
            {
                if (section.Roles[i] == "base")
                    return section.Indices[i];
            }

            // Fall back to first index
            return section.Indices[0];
        }

        public void SaveToFile(string outputPath)
        {
            File.WriteAllBytes(outputPath, _workingData);
        }

        /// <summary>
        /// Copies the raw palette bytes for a specific index from another PaletteModifier.
        /// This avoids precision loss from BGR555 to RGB conversion.
        /// </summary>
        public void CopyPaletteIndex(int index, PaletteModifier source)
        {
            int offset = index * 2;
            var sourceData = source.GetModifiedPalette();
            _workingData[offset] = sourceData[offset];
            _workingData[offset + 1] = sourceData[offset + 1];
        }
    }
}
