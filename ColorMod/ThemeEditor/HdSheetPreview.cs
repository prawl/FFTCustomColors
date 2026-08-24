using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.ThemeEditor
{
    /// <summary>
    /// Pure helpers for the HD-BMP preview path (walking a character's Images/&lt;job&gt;/original
    /// sheet, applying the live palette, and cutting out a pose or a walk-cycle frame). Split out
    /// of <see cref="PaletteModifier"/> so that class stays under the repo's line-count trigger,
    /// this file owns exactly the file-path resolution and sheet-rendering logic, nothing else.
    /// </summary>
    internal static class HdSheetPreview
    {
        private static readonly SpriteSheetExtractor SheetExtractor = new SpriteSheetExtractor();

        /// <summary>
        /// Section-mapping job names don't always match Images/ folder names.
        /// Ramza's section mappings use "RamzaCh1/RamzaCh23/RamzaCh4" but the HD
        /// BMP folders are named "RamzaChapter1/RamzaChapter23/RamzaChapter4".
        /// </summary>
        internal static string ResolveImagesFolderName(string jobName)
        {
            return jobName switch
            {
                "RamzaCh1" => "RamzaChapter1",
                "RamzaCh23" => "RamzaChapter23",
                "RamzaCh4" => "RamzaChapter4",
                _ => jobName
            };
        }

        /// <summary>
        /// Finds the HD sheet BMP for <paramref name="jobName"/> under <paramref name="modPath"/>.
        /// Returns null when the Images/&lt;job&gt;/original folder or any *.bmp inside it is
        /// missing.
        /// </summary>
        internal static string ResolveSheetPath(string modPath, string jobName)
        {
            var bmpDir = Path.Combine(modPath, "Images", jobName, "original");
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
            return bmpPath;
        }

        /// <summary>
        /// HD BMPs only carry 4 corner poses (NW/NE/SW/SE). Maps cardinals to the
        /// nearest corner so the existing 8-direction rotation cycle still works,
        /// just snapping to corners every other step.
        /// </summary>
        internal static Direction ToCornerDirection(int directionIndex)
        {
            return directionIndex switch
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
        }

        /// <summary>
        /// Renders the standing pose for <paramref name="directionIndex"/> from
        /// <paramref name="bmpPath"/> with <paramref name="modifiedPalette"/> applied.
        /// </summary>
        internal static Bitmap RenderPose(string bmpPath, string imagesFolderName, byte[] modifiedPalette, int directionIndex)
        {
            using (var themedBmp = BmpPaletteSwapper.LoadWithExternalPalette(bmpPath, modifiedPalette))
            {
                var cornerDir = ToCornerDirection(directionIndex);
                // Pass the per-character frame layout so non-standard sprites (Construct 8 = 96x96)
                // extract from the right cells rather than the default 64x80 positions.
                return SheetExtractor.ExtractSprite(themedBmp, cornerDir, FrameLayout.For(imagesFolderName));
            }
        }

        /// <summary>
        /// Renders all <see cref="FrameLayout.WalkPhaseCount"/> walk-cycle frames for
        /// <paramref name="directionIndex"/>, loading the themed BMP once and reusing it for
        /// every phase. Returns null if any phase comes back null (i.e. the layout doesn't
        /// support the walk cycle, which the caller should have already checked via
        /// <see cref="FrameLayout.SupportsWalkCycle"/>).
        /// </summary>
        internal static List<Bitmap> RenderWalkFrames(string bmpPath, string imagesFolderName, byte[] modifiedPalette, int directionIndex)
        {
            var layout = FrameLayout.For(imagesFolderName);
            var cornerDir = ToCornerDirection(directionIndex);

            using (var themedBmp = BmpPaletteSwapper.LoadWithExternalPalette(bmpPath, modifiedPalette))
            {
                var frames = new List<Bitmap>(FrameLayout.WalkPhaseCount);
                for (int phase = 0; phase < FrameLayout.WalkPhaseCount; phase++)
                {
                    var frame = SheetExtractor.ExtractWalkFrame(themedBmp, cornerDir, layout, phase);
                    if (frame == null)
                        return null;
                    frames.Add(frame);
                }
                return frames;
            }
        }
    }
}
