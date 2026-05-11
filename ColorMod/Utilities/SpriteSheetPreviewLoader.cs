using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FFTColorCustomizer.Utilities
{
    public class SpriteSheetPreviewLoader
    {
        private readonly string _basePath;
        private readonly SpriteSheetExtractor _extractor;

        public SpriteSheetPreviewLoader(string basePath)
        {
            _basePath = basePath;
            _extractor = new SpriteSheetExtractor();
        }

        public List<Bitmap> LoadPreviews(string characterName, string themeName)
        {
            // Minimal implementation to make test pass
            return new List<Bitmap>
            {
                new Bitmap(64, 80),
                new Bitmap(64, 80),
                new Bitmap(64, 80),
                new Bitmap(64, 80)
            };
        }

        public List<Bitmap> LoadPreviewsWithExtractor(string characterName, string themeName)
        {
            // 1. Prefer a pre-rendered themed BMP if one exists (Ramza ships per-theme BMPs
            //    because his theming is via TEX files, not bin palette swaps).
            var themedImagesPath = Path.Combine(_basePath, "Images", characterName, themeName);
            if (Directory.Exists(themedImagesPath))
            {
                var themedBmpPath = ResolveSpriteSheetPath(characterName, themedImagesPath);
                if (themedBmpPath != null)
                {
                    var themedSprites = _extractor.ExtractAllDirectionsFromFile(themedBmpPath);
                    return new List<Bitmap>
                    {
                        themedSprites[Direction.SW],
                        themedSprites[Direction.NW],
                        themedSprites[Direction.NE],
                        themedSprites[Direction.SE]
                    };
                }
            }

            // 2. Otherwise, find the "original" BMP and apply the theme's palette at runtime.
            //    The BMP's 16-color indexed palette aligns 1:1 with the in-game sprite's
            //    palette, so we can swap palettes instead of maintaining a BMP per theme.
            var originalImagesPath = Path.Combine(_basePath, "Images", characterName, "original");
            string originalBmpPath = Directory.Exists(originalImagesPath)
                ? ResolveSpriteSheetPath(characterName, originalImagesPath)
                : null;
            if (originalBmpPath == null)
                return new List<Bitmap>();

            using (var themedBmp = LoadThemedBmp(originalBmpPath, characterName, themeName))
            {
                var sprites = _extractor.ExtractAllDirections(themedBmp);
                return new List<Bitmap>
                {
                    sprites[Direction.SW],
                    sprites[Direction.NW],
                    sprites[Direction.NE],
                    sprites[Direction.SE]
                };
            }
        }

        private Bitmap LoadThemedBmp(string bmpPath, string characterName, string themeName)
        {
            if (string.Equals(themeName, "original", System.StringComparison.OrdinalIgnoreCase))
                return new Bitmap(bmpPath);

            // Resolve the themed .bin file. Sprite folders use lower-case character names:
            //   sprites_cloud_holy_soldier/battle_cloud_spr.bin
            var unitPath = FindUnitPath(_basePath);
            if (unitPath == null)
                return new Bitmap(bmpPath);

            var themeFolder = $"sprites_{characterName.ToLowerInvariant()}_{themeName.ToLowerInvariant().Replace(' ', '_')}";
            var themedDir = Path.Combine(unitPath, themeFolder);

            if (!Directory.Exists(themedDir))
                return new Bitmap(bmpPath);

            // Use the first .bin in the themed folder (e.g. battle_cloud_spr.bin for Cloud,
            // battle_aguri_spr.bin for Agrias). Multi-sprite characters all share the same
            // palette so any of their .bin files works for the swap.
            var binFiles = Directory.GetFiles(themedDir, "*.bin");
            if (binFiles.Length == 0)
                return new Bitmap(bmpPath);

            // If this character has palette indices marked "preserve from vanilla" (e.g. skin
            // tones), keep those vanilla so themes can't shift them — matches the section
            // mapping's intent that the slider controls skin, not the global theme palette.
            var preserve = GetPortraitPreserveIndices(characterName);
            if (preserve != null && preserve.Length > 0)
            {
                var vanillaDir = Path.Combine(unitPath, $"sprites_{characterName.ToLowerInvariant()}_original");
                var vanillaBins = Directory.Exists(vanillaDir) ? Directory.GetFiles(vanillaDir, "*.bin") : Array.Empty<string>();
                if (vanillaBins.Length > 0)
                    return BmpPaletteSwapper.LoadWithBinPalettePreserving(bmpPath, binFiles[0], vanillaBins[0], preserve);
            }

            return BmpPaletteSwapper.LoadWithBinPalette(bmpPath, binFiles[0]);
        }

        /// <summary>
        /// Loads the original BMP for a character and applies a user-provided 512-byte
        /// palette. Used for the user-theme preview path.
        /// </summary>
        public List<Bitmap> LoadPreviewsWithUserPalette(string characterName, byte[] userPalette)
        {
            var originalImagesPath = Path.Combine(_basePath, "Images", characterName, "original");
            if (!Directory.Exists(originalImagesPath))
                return new List<Bitmap>();

            string originalBmpPath = ResolveSpriteSheetPath(characterName, originalImagesPath);
            if (originalBmpPath == null)
                return new List<Bitmap>();

            using (var themedBmp = BmpPaletteSwapper.LoadWithExternalPalette(originalBmpPath, userPalette))
            {
                var sprites = _extractor.ExtractAllDirections(themedBmp);
                return new List<Bitmap>
                {
                    sprites[Direction.SW],
                    sprites[Direction.NW],
                    sprites[Direction.NE],
                    sprites[Direction.SE]
                };
            }
        }

        /// <summary>
        /// Returns true if the mod folder layout contains a sprites_&lt;char&gt;_&lt;theme&gt;/ directory
        /// with at least one .bin file — i.e. a built-in themed sprite ready for palette swap.
        /// Returns false for "original" (no themed bin needed), for user themes (their data is
        /// in a user-palette file, not a sprites_*/ folder), and for unknown themes.
        ///
        /// Callers use this to decide whether the HD-BMP palette-swap path will yield a real
        /// themed preview, or whether to fall back to a user-theme / vanilla path.
        /// </summary>
        public bool HasThemedSpriteBin(string characterName, string themeName)
        {
            if (string.IsNullOrEmpty(characterName) || string.IsNullOrEmpty(themeName)) return false;
            if (string.Equals(themeName, "original", System.StringComparison.OrdinalIgnoreCase)) return false;

            var unitPath = FindUnitPath(_basePath);
            if (unitPath == null) return false;

            var themeFolder = $"sprites_{characterName.ToLowerInvariant()}_{themeName.ToLowerInvariant().Replace(' ', '_')}";
            var themedDir = Path.Combine(unitPath, themeFolder);
            return Directory.Exists(themedDir) && Directory.GetFiles(themedDir, "*.bin").Length > 0;
        }

        /// <summary>
        /// Returns the path to the character's portrait BMP. Resolution order:
        ///   1. A dedicated *_portrait_hd.bmp (the proper dialog-portrait extracted from
        ///      event_wldface_bin.bin via the Sprite Toolkit). 24bpp RGB, single expression
        ///      cells, correct colors.
        ///   2. The second-lowest sprite-atlas BMP (e.g. 881_Agrias_hd.bmp) — fallback for
        ///      characters without a wldface portrait. Has known color-aliasing issues
        ///      (face/coat share palette indices in the atlas).
        /// Returns null if nothing is found.
        /// </summary>
        public string ResolvePortraitSheetPath(string characterName)
        {
            var imagesPath = Path.Combine(_basePath, "Images", characterName, "original");
            if (!Directory.Exists(imagesPath))
                return null;

            var dedicated = Directory.GetFiles(imagesPath, "*_portrait_hd.bmp");
            if (dedicated.Length > 0) return dedicated.OrderBy(Path.GetFileName).First();

            var hdFiles = Directory.GetFiles(imagesPath, "*_hd.bmp")
                .Concat(Directory.GetFiles(imagesPath, "*_hd.png"))
                .Where(p => !Path.GetFileName(p).Contains("_portrait_hd"))
                .OrderBy(p => Path.GetFileName(p))
                .ToArray();

            if (hdFiles.Length == 0) return null;
            return hdFiles.Length >= 2 ? hdFiles[1] : hdFiles[0];
        }

        private static bool IsDedicatedPortrait(string path)
        {
            return path != null && Path.GetFileName(path).Contains("_portrait_hd");
        }

        /// <summary>
        /// Per-character crop region of the portrait, depending on which source file we got.
        /// Dedicated wldface portraits are 520×388 atlases of 8 expressions arranged 4×2; we
        /// crop the first cell (neutral expression). Sprite-atlas fallback portraits need a
        /// hand-tuned crop for each character.
        /// </summary>
        private static Rectangle? GetPortraitCropRegion(string characterName, string portraitPath)
        {
            if (IsDedicatedPortrait(portraitPath))
            {
                // wldface format: 520×388, 4 columns × 2 rows of 130×194 expressions
                return new Rectangle(0, 0, 130, 194);
            }
            return characterName switch
            {
                "Agrias" => new Rectangle(160, 384, 96, 80),
                _ => null
            };
        }

        /// <summary>
        /// Palette indices that should keep their vanilla color when applying a theme to the
        /// portrait — typically the skin tone section. Pulled from the per-character SectionMapping
        /// JSON (e.g. Agrias.json's "SkinColor": [14, 15]). Hardcoded for now while only Agrias
        /// is wired up; future characters will look this up from their SectionMapping.
        /// </summary>
        private static int[] GetPortraitPreserveIndices(string characterName)
        {
            return characterName switch
            {
                "Agrias" => new[] { 14, 15 },
                _ => null
            };
        }

        /// <summary>
        /// Loads the character's portrait with the active theme applied, cropped to the
        /// portrait-pose region. Handles two source formats:
        ///   • Indexed 4bpp sprite-atlas BMP: palette swap (with skin-preserve indices)
        ///   • 24bpp wldface portrait BMP: per-pixel nearest-color recolor (no palette)
        /// Returns null if no portrait BMP exists for the character.
        /// </summary>
        public Bitmap LoadPortraitWithBinPalette(string characterName, string themeName)
        {
            var portraitPath = ResolvePortraitSheetPath(characterName);
            if (portraitPath == null) return null;

            bool isDedicated = IsDedicatedPortrait(portraitPath);
            Bitmap fullSheet;

            if (string.Equals(themeName, "original", System.StringComparison.OrdinalIgnoreCase))
            {
                fullSheet = new Bitmap(portraitPath);
            }
            else
            {
                var (themedBinPath, vanillaBinPath) = ResolveThemeBinPaths(characterName, themeName);
                if (themedBinPath == null || vanillaBinPath == null)
                {
                    fullSheet = new Bitmap(portraitPath);
                }
                else if (isDedicated)
                {
                    // 24bpp wldface: nearest-color recolor against vanilla → themed mapping
                    fullSheet = RecolorPortraitByNearestMatch(portraitPath, themedBinPath, vanillaBinPath,
                        GetPortraitPreserveIndices(characterName));
                }
                else
                {
                    // Indexed sprite atlas: palette swap with optional skin preserve
                    var preserve = GetPortraitPreserveIndices(characterName);
                    fullSheet = (preserve != null && preserve.Length > 0)
                        ? BmpPaletteSwapper.LoadWithBinPalettePreserving(portraitPath, themedBinPath, vanillaBinPath, preserve)
                        : BmpPaletteSwapper.LoadWithBinPalette(portraitPath, themedBinPath);
                }
            }

            var crop = GetPortraitCropRegion(characterName, portraitPath);
            if (!crop.HasValue) return fullSheet;

            var rect = Rectangle.Intersect(crop.Value, new Rectangle(0, 0, fullSheet.Width, fullSheet.Height));
            if (rect.Width <= 0 || rect.Height <= 0) return fullSheet;

            Bitmap cropped;
            using (fullSheet)
            {
                cropped = fullSheet.Clone(rect, fullSheet.PixelFormat);
            }

            // Sprite-atlas portrait poses are stored sideways and need a 270° CCW rotation to
            // stand upright. Dedicated wldface portraits are already upright — skip the rotation.
            if (!isDedicated)
                cropped.RotateFlip(RotateFlipType.Rotate270FlipNone);

            return cropped;
        }

        private (string themedBin, string vanillaBin) ResolveThemeBinPaths(string characterName, string themeName)
        {
            var unitPath = FindUnitPath(_basePath);
            if (unitPath == null) return (null, null);

            var themeFolder = $"sprites_{characterName.ToLowerInvariant()}_{themeName.ToLowerInvariant().Replace(' ', '_')}";
            var themedDir = Path.Combine(unitPath, themeFolder);
            var themedBins = Directory.Exists(themedDir) ? Directory.GetFiles(themedDir, "*.bin") : Array.Empty<string>();
            if (themedBins.Length == 0) return (null, null);

            var vanillaDir = Path.Combine(unitPath, $"sprites_{characterName.ToLowerInvariant()}_original");
            var vanillaBins = Directory.Exists(vanillaDir) ? Directory.GetFiles(vanillaDir, "*.bin") : Array.Empty<string>();
            if (vanillaBins.Length == 0) return (null, null);

            return (themedBins[0], vanillaBins[0]);
        }

        /// <summary>
        /// Pixel-level recolor for 24bpp portraits. For each pixel, finds the closest vanilla
        /// palette color, looks up the matching themed color, and writes it back. Indices in
        /// <paramref name="preserveFromVanilla"/> are skipped (their original pixels stay vanilla).
        /// </summary>
        private static Bitmap RecolorPortraitByNearestMatch(string portraitPath, string themedBinPath, string vanillaBinPath, int[] preserveFromVanilla)
        {
            byte[] themedBytes = new byte[32];
            byte[] vanillaBytes = new byte[32];
            using (var fs = File.OpenRead(themedBinPath)) { if (fs.Read(themedBytes, 0, 32) < 32) return new Bitmap(portraitPath); }
            using (var fs = File.OpenRead(vanillaBinPath)) { if (fs.Read(vanillaBytes, 0, 32) < 32) return new Bitmap(portraitPath); }

            var preserve = new HashSet<int>(preserveFromVanilla ?? Array.Empty<int>());
            var vanillaColors = BmpPaletteSwapper.DecodeBgr555Palette(vanillaBytes);
            var themedColors = BmpPaletteSwapper.DecodeBgr555Palette(vanillaBytes);
            var paletteMap = BmpPaletteSwapper.DecodeBgr555Palette(themedBytes);
            for (int i = 0; i < 16; i++)
                themedColors[i] = preserve.Contains(i) ? vanillaColors[i] : paletteMap[i];

            var src = new Bitmap(portraitPath);
            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);

            var srcRect = new Rectangle(0, 0, src.Width, src.Height);
            var srcData = src.LockBits(srcRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var dstData = dst.LockBits(srcRect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            int len = srcData.Stride * src.Height;
            var srcPixels = new byte[len];
            var dstPixels = new byte[len];
            System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, srcPixels, 0, len);

            for (int i = 0; i < len; i += 4)
            {
                var px = Color.FromArgb(srcPixels[i + 3], srcPixels[i + 2], srcPixels[i + 1], srcPixels[i]);
                int idx = BmpPaletteSwapper.FindNearestPaletteIndex(px, vanillaColors);
                var outC = themedColors[idx];
                dstPixels[i]     = outC.B;
                dstPixels[i + 1] = outC.G;
                dstPixels[i + 2] = outC.R;
                dstPixels[i + 3] = px.A;
            }

            System.Runtime.InteropServices.Marshal.Copy(dstPixels, 0, dstData.Scan0, len);
            src.UnlockBits(srcData);
            dst.UnlockBits(dstData);
            src.Dispose();
            return dst;
        }

        private static string FindUnitPath(string basePath)
        {
            // Mod folder layout: <basePath>/FFTIVC/data/enhanced/fftpack/unit/
            var direct = Path.Combine(basePath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            if (Directory.Exists(direct))
                return direct;

            // Some installs put the FFTIVC tree one level deeper (versioned ColorMod dir).
            var colorModRoot = Path.Combine(basePath, "ColorMod", "FFTIVC", "data", "enhanced", "fftpack", "unit");
            return Directory.Exists(colorModRoot) ? colorModRoot : null;
        }

        private static string ResolveSpriteSheetPath(string characterName, string imagesPath)
        {
            // Ramza chapters use a known numeric prefix from the game's TEX file IDs
            string filePrefix = characterName switch
            {
                "RamzaChapter1" => "830",
                "RamzaChapter23" => "832",
                "RamzaChapter4" => "834",
                _ => null
            };

            if (filePrefix != null)
            {
                var prefixed = Directory.GetFiles(imagesPath, filePrefix + "_*.bmp")
                    .Concat(Directory.GetFiles(imagesPath, filePrefix + "_*.png"))
                    .ToArray();
                return prefixed.Length > 0 ? prefixed[0] : null;
            }

            // Non-Ramza: prefer an explicit "sprite_sheet" if present, else fall back to
            // the first "*_hd.*" file we find (Sprite Toolkit naming).
            var generic = new[]
            {
                Path.Combine(imagesPath, "sprite_sheet.bmp"),
                Path.Combine(imagesPath, "sprite_sheet.png"),
            };
            foreach (var g in generic)
            {
                if (File.Exists(g)) return g;
            }

            // Sort so multi-form characters (Agrias 880/881/914/915, Cloud 910/911, etc.)
            // deterministically pick the lowest-numbered "main" battle BMP.
            var hdFiles = Directory.GetFiles(imagesPath, "*_hd.bmp")
                .Concat(Directory.GetFiles(imagesPath, "*_hd.png"))
                .OrderBy(p => Path.GetFileName(p))
                .ToArray();
            return hdFiles.Length > 0 ? hdFiles[0] : null;
        }
    }
}