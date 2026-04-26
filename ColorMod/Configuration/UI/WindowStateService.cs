using System;
using System.IO;
using System.Text.Json;

namespace FFTColorCustomizer.Configuration.UI
{
    /// <summary>
    /// Persists the configuration window's last-used size to a small JSON file
    /// next to Config.json so users don't have to resize on every launch.
    /// Kept separate from Config.json to avoid touching the reflection-based
    /// theme converter.
    /// </summary>
    public static class WindowStateService
    {
        public const string FileName = "WindowState.json";

        public class WindowState
        {
            public int Width { get; set; }
            public int Height { get; set; }
        }

        public static string? GetStatePath(string? configPath)
        {
            if (string.IsNullOrEmpty(configPath)) return null;
            var dir = Path.GetDirectoryName(configPath);
            if (string.IsNullOrEmpty(dir)) return null;
            return Path.Combine(dir, FileName);
        }

        public static WindowState? Load(string? configPath)
        {
            var path = GetStatePath(configPath);
            if (path == null || !File.Exists(path)) return null;

            try
            {
                var json = File.ReadAllText(path);
                var state = JsonSerializer.Deserialize<WindowState>(json);
                return state != null && state.Width > 0 && state.Height > 0 ? state : null;
            }
            catch
            {
                return null;
            }
        }

        public static void Save(string? configPath, int width, int height)
        {
            var path = GetStatePath(configPath);
            if (path == null) return;

            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(
                    new WindowState { Width = width, Height = height },
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch
            {
                // Persistence is best-effort: a failure to save the window size
                // must never block closing the configuration form.
            }
        }

        /// <summary>
        /// Resolves the size to apply on form open. Saved values are clamped to
        /// the supplied min/max bounds so a stale entry from a 4K monitor can't
        /// open off-screen on a smaller display, and a corrupt tiny value can't
        /// produce an unusable form.
        /// </summary>
        public static (int width, int height) ResolveSize(
            WindowState? saved,
            int defaultWidth, int defaultHeight,
            int minWidth, int minHeight,
            int maxWidth, int maxHeight)
        {
            int w = saved?.Width ?? defaultWidth;
            int h = saved?.Height ?? defaultHeight;
            return (Clamp(w, minWidth, maxWidth), Clamp(h, minHeight, maxHeight));
        }

        private static int Clamp(int value, int min, int max)
        {
            if (max < min) max = min;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
