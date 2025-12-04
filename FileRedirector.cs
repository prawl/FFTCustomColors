namespace FFTColorMod
{
    public enum ColorScheme
    {
        Original = 0,
        Blue = 1,
        Red = 2,
        Green = 3,
        Purple = 4
    }

    public class FileRedirector
    {
        // TLDR: Manages file redirection for color scheme switching

        public ColorScheme ActiveScheme { get; private set; } = ColorScheme.Original;

        public void SetActiveColorScheme(ColorScheme scheme)
        {
            // TLDR: Switch to specified color scheme
            ActiveScheme = scheme;
        }
    }
}