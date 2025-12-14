using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FFTColorMod.Configuration.UI
{
    public class PreviewImageManager
    {
        private readonly string _modPath;

        public PreviewImageManager(string modPath)
        {
            _modPath = modPath;
        }

        public void UpdateJobPreview(PictureBox pictureBox, string jobName, ColorScheme theme)
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
                Console.WriteLine($"[FFT Color Mod] Error loading preview for {jobName} ({theme}): {ex.Message}");
                ClearPreview(pictureBox);
            }
        }

        public void UpdateStoryCharacterPreview(PictureBox pictureBox, string characterName, string theme)
        {
            try
            {
                string imagePath = GetStoryCharacterPreviewPath(characterName, theme);

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
                Console.WriteLine($"[FFT Color Mod] Error loading preview for {characterName} ({theme}): {ex.Message}");
                ClearPreview(pictureBox);
            }
        }

        private string GetJobPreviewPath(string jobName, ColorScheme theme)
        {
            string themeFolder = theme == ColorScheme.original ? "sprites_original" : $"sprites_{theme}";
            string baseName = jobName.ToLower().Replace(" ", "_");

            // Try different patterns for the preview image
            string[] patterns = {
                Path.Combine(_modPath, "previews", themeFolder, $"{baseName}_preview.png"),
                Path.Combine(_modPath, "previews", themeFolder, $"{baseName}.png"),
                Path.Combine(_modPath, "previews", themeFolder, $"battle_{baseName}_m_preview.png"),
                Path.Combine(_modPath, "previews", themeFolder, $"battle_{baseName}_f_preview.png")
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
            string themeFolder = $"sprites_{baseName}_{theme.ToLower()}";

            // Try different preview paths
            string[] patterns = {
                Path.Combine(_modPath, "previews", themeFolder, $"{baseName}_preview.png"),
                Path.Combine(_modPath, "previews", themeFolder, "preview.png"),
                Path.Combine(_modPath, "previews", $"{baseName}_{theme.ToLower()}_preview.png")
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