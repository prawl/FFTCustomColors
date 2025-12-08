using System;

namespace FFTColorMod
{
    public enum ColorScheme
    {
        Original = 0,
        WhiteSilver = 1,
        OceanBlue = 2,
        DeepPurple = 3
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

        public string GetRedirectedPath(string originalPath)
        {
            // TLDR: Get path with color suffix based on active scheme
            if (ActiveScheme == ColorScheme.Original)
            {
                return originalPath;
            }

            // TLDR: Map enum to folder names with underscores
            string colorFolder = ActiveScheme switch
            {
                ColorScheme.WhiteSilver => "white_silver",
                ColorScheme.OceanBlue => "ocean_blue",
                ColorScheme.DeepPurple => "deep_purple",
                _ => "original"
            };

            // TLDR: Insert color suffix before file extension
            var lastDot = originalPath.LastIndexOf('.');
            if (lastDot == -1)
            {
                return originalPath + "_" + colorFolder;
            }

            var basePath = originalPath.Substring(0, lastDot);
            var extension = originalPath.Substring(lastDot);
            return basePath + "_" + colorFolder + extension;
        }
    }
}