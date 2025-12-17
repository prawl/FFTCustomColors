using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Configuration.UI
{
    public class PreviewImageManager
    {
        private readonly string _modPath;

        public PreviewImageManager(string modPath)
        {
            _modPath = modPath;
            Console.WriteLine($"[FFT Color Mod] PreviewImageManager initialized with modPath: {modPath}");

            // Log what preview path we'll be looking in
            var previewPath = Path.Combine(modPath, "Resources", "Previews");
            Console.WriteLine($"[FFT Color Mod] Will look for previews in: {previewPath}");
            ModLogger.Log($"PreviewImageManager initialized with path: {modPath}");
        }

        public void UpdateJobPreview(PictureBox pictureBox, string jobName, string theme)
        {
            try
            {
                string imagePath = GetJobPreviewPath(jobName, theme);

                if (File.Exists(imagePath))
                {
                    using (var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                    using (var img = Image.FromStream(stream))
                    {
                        // Scale the image up while maintaining pixel art look
                        var scaledImage = new Bitmap(img.Width * 2, img.Height * 2);
                        using (var g = Graphics.FromImage(scaledImage))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                            g.DrawImage(img, 0, 0, scaledImage.Width, scaledImage.Height);
                        }
                        pictureBox.Image?.Dispose();
                        pictureBox.Image = scaledImage;
                    }
                }
                else
                {
                    ClearPreview(pictureBox);
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"loading preview for {jobName} ({theme}): {ex.Message}");
                ClearPreview(pictureBox);
            }
        }

        public void UpdateStoryCharacterPreview(PictureBox pictureBox, string characterName, string theme)
        {
            try
            {
                string imagePath = GetStoryCharacterPreviewPath(characterName, theme);
                Console.WriteLine($"[FFT Color Mod] Looking for story character preview at: {imagePath}");
                ModLogger.LogDebug($"Looking for story character preview at: {imagePath}");

                if (File.Exists(imagePath))
                {
                    ModLogger.Log($"Found preview image: {imagePath}");
                    using (var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                    using (var img = Image.FromStream(stream))
                    {
                        // Scale the image up while maintaining pixel art look
                        var scaledImage = new Bitmap(img.Width * 2, img.Height * 2);
                        using (var g = Graphics.FromImage(scaledImage))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                            g.DrawImage(img, 0, 0, scaledImage.Width, scaledImage.Height);
                        }
                        pictureBox.Image?.Dispose();
                        pictureBox.Image = scaledImage;
                    }
                }
                else
                {
                    ModLogger.Log($"Preview image NOT found at: {imagePath}");
                    ClearPreview(pictureBox);
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"loading preview for {characterName} ({theme}): {ex.Message}");
                ClearPreview(pictureBox);
            }
        }

        private string GetJobPreviewPath(string jobName, string theme)
        {
            string baseName = jobName.ToLower().Replace(" ", "_");
            string themeName = theme.ToLower();

            // Try different patterns for the preview image (flat structure in Resources/Previews)
            string[] patterns = {
                // Direct path in Resources/Previews folder (deployed location)
                Path.Combine(_modPath, "Resources", "Previews", $"{baseName}_{themeName}.png"),
                // Legacy paths for backwards compatibility
                Path.Combine(_modPath, "Previews", $"{baseName}_{themeName}.png"),
                Path.Combine(_modPath, "previews", $"{baseName}_{themeName}.png")
            };

            foreach (var pattern in patterns)
            {
                if (File.Exists(pattern))
                    return pattern;
            }

            return patterns[0]; // Return first pattern even if not found
        }

        private string GetStoryCharacterPreviewPath(string characterName, string theme)
        {
            string baseName = characterName.ToLower();

            // Try different preview paths - prioritize the actual deployed location
            string[] patterns = {
                // Actual deployed location in ColorMod/Resources/Previews/
                Path.Combine(_modPath, "Resources", "Previews", $"{baseName}_{theme.ToLower()}.png"),
                Path.Combine(_modPath, "Resources", "Previews", $"{baseName}_{theme.ToLower()}_preview.png"),
                // Fallback paths for compatibility
                Path.Combine(_modPath, "previews", $"{baseName}_{theme.ToLower()}.png"),
                Path.Combine(_modPath, "Previews", $"{baseName}_{theme.ToLower()}.png")
            };

            foreach (var pattern in patterns)
            {
                if (File.Exists(pattern))
                    return pattern;
            }

            return patterns[0];
        }

        private void ClearPreview(PictureBox pictureBox)
        {
            pictureBox.Image?.Dispose();
            pictureBox.Image = null;
            pictureBox.BackColor = Color.FromArgb(45, 45, 45);
        }
    }
}
