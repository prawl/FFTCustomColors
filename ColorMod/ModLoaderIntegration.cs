using System.Windows.Forms;

namespace FFTColorMod
{
    public class ModLoaderIntegration
    {
        // TLDR: Integrates with FFTIVC modloader for file redirection

        public ColorScheme CurrentColorScheme { get; private set; }
        public int RescanCount { get; private set; }
        public FileRedirector FileRedirector { get; private set; }
        private ColorPreferencesManager _preferencesManager;

        public ModLoaderIntegration()
        {
            // TLDR: Initialize with FileRedirector
            FileRedirector = new FileRedirector();
        }

        public ModLoaderIntegration(string preferencesPath) : this()
        {
            // TLDR: Initialize with preferences path and auto-load
            SetPreferencesPath(preferencesPath);
            LoadPreferences();
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
            // TLDR: Handle hotkey press for color switching and save preferences
            if (key == Keys.F1)
            {
                // Cycle through colors: Original -> WhiteSilver -> OceanBlue -> DeepPurple -> Original
                ColorScheme nextScheme = CurrentColorScheme switch
                {
                    ColorScheme.Original => ColorScheme.WhiteSilver,
                    ColorScheme.WhiteSilver => ColorScheme.OceanBlue,
                    ColorScheme.OceanBlue => ColorScheme.DeepPurple,
                    ColorScheme.DeepPurple => ColorScheme.Original,
                    _ => ColorScheme.Original
                };
                SetColorScheme(nextScheme);
                SavePreferences();
            }
            else if (key == Keys.F9)
            {
                RescanCount++;
            }
        }

        private void SavePreferences()
        {
            // TLDR: Save current color scheme to preferences
            _preferencesManager?.SavePreferences(CurrentColorScheme);
        }

        public void SetPreferencesPath(string path)
        {
            // TLDR: Set path for preferences file
            _preferencesManager = new ColorPreferencesManager(path);
        }

        public void LoadPreferences()
        {
            // TLDR: Load saved preferences
            if (_preferencesManager != null)
            {
                CurrentColorScheme = _preferencesManager.LoadPreferences();
                FileRedirector.SetActiveColorScheme(CurrentColorScheme);
            }
        }
    }
}