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

        /// <summary>
        /// The dropdown entry the user actually picked (e.g. "Bonesnatch (rank 2)"). For a
        /// monster, <see cref="JobName"/> is the FAMILY key ("Skeleton") since the save stays
        /// family scoped by design — this is what a confirmation message should name instead.
        /// Defaults to <see cref="JobName"/> for every existing caller that doesn't pass one.
        /// See CC-27.
        /// </summary>
        public string DisplayName { get; }

        public ThemeSavedEventArgs(string jobName, string themeName, byte[] paletteData, string displayName = null)
        {
            JobName = jobName;
            ThemeName = themeName;
            PaletteData = paletteData;
            DisplayName = displayName ?? jobName;
        }
    }
}
