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

        /// <summary>
        /// Which palette in the bin the editor is reading and writing. Monster families pack their
        /// three ranks into one bin as palettes 0/1/2, so rank II and rank III are only reachable
        /// by setting this. Defaults to 0, which is rank I and the only palette single-palette
        /// sprites have, so every non-monster caller behaves exactly as before. See CC-27.
        /// </summary>
        public int PaletteIndex { get; set; }

        private const int PaletteSizeBytes = 32;   // 16 colors * 2 bytes (BGR555)

        /// <summary>Byte offset of the palette currently selected by <see cref="PaletteIndex"/>.</summary>
        private int PaletteOffset => PaletteIndex * PaletteSizeBytes;

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

            // Write into the SELECTED palette, not unconditionally palette 0 (CC-27)
            int offset = PaletteOffset + index * 2;
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
            int offset = PaletteOffset + index * 2;
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

        /// <summary>
        /// The palette block to persist for a saved theme. The SELECTED palette is placed at
        /// offset 0, because a saved theme is a flat set of section colors and
        /// <c>MonsterRecolor.ApplyUserPaletteSection</c> reads it as palette 0. With the default
        /// <see cref="PaletteIndex"/> of 0 this is byte-identical to a straight copy, so existing
        /// themes and every single-palette caller are unaffected. See CC-27.
        /// </summary>
        public byte[] GetModifiedPalette()
        {
            var palette = new byte[PaletteSizeBytes * 16]; // 16 palettes fit in the 512 byte block
            Array.Copy(_workingData, 0, palette, 0, palette.Length);
            if (PaletteIndex != 0)
                Array.Copy(_workingData, PaletteOffset, palette, 0, PaletteSizeBytes);
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
            // Try the HD BMP path with the live-edited palette. Returns null if no BMP
            // is available for this character/job (falls back to chunky bin extraction).
            // Construct 8 also goes through HD now that 1108_Automaton_hd.bmp ships and
            // SpriteSheetExtractor knows its 96x96 frame layout.
            var hdPreview = TryGetHdPreview(directionIndex);
            if (hdPreview != null)
                return hdPreview;

            // Construct 8 / tetsu has a non-standard sprite layout in the bin — use the
            // custom rect (48x48 at x=48) when the HD BMP path isn't available.
            if (string.Equals(_jobName, "Construct8", StringComparison.OrdinalIgnoreCase))
            {
                return _extractor.ExtractCustomRect(_workingData, xOffset: 48, yOffset: 0, srcWidth: 48, srcHeight: 48, paletteIndex: PaletteIndex);
            }

            // ExtractAllDirections returns sprites indexed by compass direction:
            // 0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW
            var sprites = _extractor.ExtractAllDirections(_workingData, characterIndex: 0, paletteIndex: PaletteIndex);
            return sprites[directionIndex];
        }

        /// <summary>
        /// Builds a high-resolution preview by applying the live-edited palette to the
        /// character's HD sprite-sheet BMP. Uses <see cref="GetModifiedPalette"/> rather than
        /// the raw working data, since that already places the SELECTED palette (see
        /// <see cref="PaletteIndex"/>) at offset 0 — exactly the 32 bytes the swapper reads.
        /// Returns null if no HD BMP is found — caller falls back to bin extraction.
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

            // Apply the live palette. GetModifiedPalette() places the selected rank's 16
            // BGR555 colors at offset 0, which is what the swapper reads (CC-27).
            using (var themedBmp = BmpPaletteSwapper.LoadWithExternalPalette(bmpPath, GetModifiedPalette()))
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
                // Pass the per-character frame layout so non-standard sprites (Construct 8 = 96x96)
                // extract from the right cells rather than the default 64x80 positions.
                return _sheetExtractor.ExtractSprite(themedBmp, cornerDir, FrameLayout.For(imagesFolderName));
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

            var generator = new RelativeShadeGenerator(originalColors, primaryIndex, section.ShadeMode);
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
        /// This avoids precision loss from BGR555 to RGB conversion. Writes into THIS
        /// modifier's SELECTED palette (<see cref="PaletteOffset"/>), not unconditionally
        /// palette 0 — otherwise resetting a section on rank II/III would silently write into
        /// the untouched rank I bytes and leave the visible (selected-rank) colour unchanged.
        /// <paramref name="source"/>.GetModifiedPalette() already normalizes ITS OWN selected
        /// palette to offset 0, so the read side stays a plain <c>index * 2</c>. See CC-27.
        /// </summary>
        public void CopyPaletteIndex(int index, PaletteModifier source)
        {
            int srcOffset = index * 2;
            int destOffset = PaletteOffset + srcOffset;
            var sourceData = source.GetModifiedPalette();
            _workingData[destOffset] = sourceData[srcOffset];
            _workingData[destOffset + 1] = sourceData[srcOffset + 1];
        }
    }
}
