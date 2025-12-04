using System;

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

        public string GetRedirectedPath(string originalPath)
        {
            // TLDR: Get path with color suffix based on active scheme
            if (ActiveScheme == ColorScheme.Original)
            {
                return originalPath;
            }

            // TLDR: Insert color suffix before file extension
            var lastDot = originalPath.LastIndexOf('.');
            if (lastDot == -1)
            {
                return originalPath + "_" + ActiveScheme.ToString().ToLower();
            }

            var basePath = originalPath.Substring(0, lastDot);
            var extension = originalPath.Substring(lastDot);
            return basePath + "_" + ActiveScheme.ToString().ToLower() + extension;
        }
    }
}