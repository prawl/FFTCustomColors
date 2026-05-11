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
        private string _jobName;
        private string _modPath;
        private readonly BinSpriteExtractor _extractor = new BinSpriteExtractor();
        private readonly SpriteSheetExtractor _sheetExtractor = new SpriteSheetExtractor();
        private readonly Dictionary<string, RelativeShadeGenerator> _shadeGenerators = new Dictionary<string, RelativeShadeGenerator>();

        public bool IsLoaded { get; private set; }

        public void LoadTemplate(string binPath, string jobName = null, string modPath = null)
        {
            if (!File.Exists(binPath))
                throw new FileNotFoundException($"Template file not found: {binPath}");

            _originalData = File.ReadAllBytes(binPath);
            _workingData = (byte[])_originalData.Clone();
            _jobName = jobName;
            _modPath = modPath;
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
            // Construct 8 / tetsu has a non-standard sprite layout — use the custom rect
            // and ignore directionIndex (rotate is a no-op for this character).
            if (string.Equals(_jobName, "Construct8", StringComparison.OrdinalIgnoreCase))
            {
                return _extractor.ExtractCustomRect(_workingData, xOffset: 48, yOffset: 0, srcWidth: 48, srcHeight: 48, paletteIndex: 0);
            }

            // Try the HD BMP path with the live-edited palette. Returns null if no BMP
            // is available for this character/job (falls back to chunky bin extraction).
            var hdPreview = TryGetHdPreview(directionIndex);
            if (hdPreview != null)
                return hdPreview;

            // ExtractAllDirections returns sprites indexed by compass direction:
            // 0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW
            var sprites = _extractor.ExtractAllDirections(_workingData, characterIndex: 0, paletteIndex: 0);
            return sprites[directionIndex];
        }

        /// <summary>
        /// Builds a high-resolution preview by applying the live-edited palette (the first
        /// 32 bytes of _workingData) to the character's HD sprite-sheet BMP. Returns null
        /// if no HD BMP is found — caller falls back to bin extraction.
        /// </summary>
        private Bitmap TryGetHdPreview(int directionIndex)
        {
            if (string.IsNullOrEmpty(_modPath) || string.IsNullOrEmpty(_jobName))
                return null;

            // Section-mapping job names don't always match Images/ folder names.
            // Ramza's section mappings use "RamzaCh1/RamzaCh23/RamzaCh4" but the HD
            // BMP folders are named "RamzaChapter1/RamzaChapter23/RamzaChapter4".
            var imagesFolderName = _jobName switch
            {
                "RamzaCh1" => "RamzaChapter1",
                "RamzaCh23" => "RamzaChapter23",
                "RamzaCh4" => "RamzaChapter4",
                _ => _jobName
            };

            var bmpDir = Path.Combine(_modPath, "Images", imagesFolderName, "original");
            if (!Directory.Exists(bmpDir))
                return null;

            // Most characters use the Sprite Toolkit's "<id>_<Name>_hd.bmp" naming, but
            // Ramza ships pre-rendered "<id>_Ramuza_ChN.bmp" (no _hd suffix). Accept both.
            string bmpPath = null;
            foreach (var f in Directory.GetFiles(bmpDir, "*.bmp"))
            {
                if (bmpPath == null || string.CompareOrdinal(Path.GetFileName(f), Path.GetFileName(bmpPath)) < 0)
                    bmpPath = f;
            }
            if (bmpPath == null)
                return null;

            // Apply the live palette (first 32 bytes of working data = 16 BGR555 colors)
            using (var themedBmp = BmpPaletteSwapper.LoadWithExternalPalette(bmpPath, _workingData))
            {
                // HD BMPs only carry 4 corner poses (NW/NE/SW/SE). Map cardinals to the
                // nearest corner so the existing 8-direction rotation cycle still works,
                // just snapping to corners every other step.
                var cornerDir = directionIndex switch
                {
                    0 => Direction.NW, // N
                    1 => Direction.NE, // NE
                    2 => Direction.NE, // E
                    3 => Direction.SE, // SE
                    4 => Direction.SW, // S
                    5 => Direction.SW, // SW
                    6 => Direction.NW, // W
                    7 => Direction.NW, // NW
                    _ => Direction.SW
                };
                return _sheetExtractor.ExtractSprite(themedBmp, cornerDir);
            }
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
