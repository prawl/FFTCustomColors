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
            // TLDR: Register file redirection using FileRedirector
            if (redirectedPath == null)
            {
                // TLDR: Auto-generate redirected path based on color scheme
                redirectedPath = FileRedirector.GetRedirectedPath(originalPath);
            }
            return true;
        }

        public string GetRedirectedPath(string originalPath)
        {
            // TLDR: Get the redirected path for the current color scheme
            return FileRedirector.GetRedirectedPath(originalPath);
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