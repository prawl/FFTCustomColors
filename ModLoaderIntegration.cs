using System.Windows.Forms;

namespace FFTColorMod
{
    public class ModLoaderIntegration
    {
        // TLDR: Integrates with FFTIVC modloader for file redirection

        public ColorScheme CurrentColorScheme { get; private set; }
        public int RescanCount { get; private set; }
        public FileRedirector FileRedirector { get; private set; }

        public ModLoaderIntegration()
        {
            // TLDR: Initialize with FileRedirector
            FileRedirector = new FileRedirector();
        }

        public bool RegisterFileRedirect(string originalPath, string redirectedPath)
        {
            // TLDR: Register file redirection - minimal implementation
            return true;
        }

        public void SetColorScheme(ColorScheme scheme)
        {
            // TLDR: Update active color scheme
            CurrentColorScheme = scheme;
            FileRedirector.SetActiveColorScheme(scheme);
        }

        public void ProcessHotkey(Keys key)
        {
            // TLDR: Handle hotkey press for color switching
            if (key == Keys.F1)
            {
                SetColorScheme(ColorScheme.Blue);
            }
            else if (key == Keys.F2)
            {
                SetColorScheme(ColorScheme.Red);
            }
            else if (key == Keys.F4)
            {
                SetColorScheme(ColorScheme.Purple);
            }
            else if (key == Keys.F7)
            {
                SetColorScheme(ColorScheme.Green);
            }
            else if (key == Keys.F8)
            {
                SetColorScheme(ColorScheme.Original);
            }
            else if (key == Keys.F9)
            {
                RescanCount++;
            }
        }
    }
}