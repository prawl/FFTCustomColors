using System;

namespace FFTColorCustomizer.ThemeEditor
{
    /// <summary>
    /// Event arguments for theme saved events.
    /// </summary>
    public class ThemeSavedEventArgs : EventArgs
    {
        public string JobName { get; }
        public string ThemeName { get; }
        public byte[] PaletteData { get; }

        public ThemeSavedEventArgs(string jobName, string themeName, byte[] paletteData)
        {
            JobName = jobName;
            ThemeName = themeName;
            PaletteData = paletteData;
        }
    }
}
