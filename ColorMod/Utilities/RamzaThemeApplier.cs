using System.Drawing;

namespace FFTColorCustomizer.Utilities
{
    /// <summary>
    /// Applies theme color transformations to Ramza sprite previews
    /// </summary>
    public class RamzaThemeApplier
    {
        public Bitmap ApplyTheme(Bitmap originalBitmap, string themeName)
        {
            // For now, just return a copy of the original
            // We'll implement the actual color transformation later
            return new Bitmap(originalBitmap);
        }
    }
}