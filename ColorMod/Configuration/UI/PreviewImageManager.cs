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
            ModLogger.Log($"PreviewImageManager initialized with modPath: {modPath}");
            // Note: We no longer use PNG preview files - this class is kept for compatibility
        }

        /// <summary>
        /// Checks if the manager has a valid mod path with FFTIVC directory
        /// </summary>
        public bool HasValidModPath()
        {
            if (string.IsNullOrEmpty(_modPath))
                return false;

            // Check for FFTIVC directory which contains the BIN files
            var fftivcPath = Path.Combine(_modPath, "FFTIVC", "data", "enhanced", "fftpack", "unit");
            return Directory.Exists(fftivcPath);
        }

        public void UpdateJobPreview(PictureBox pictureBox, string jobName, string theme)
        {
            // We no longer have preview images - just clear the preview box
            ClearPreview(pictureBox);
        }

        public void UpdateStoryCharacterPreview(PictureBox pictureBox, string characterName, string theme)
        {
            // We no longer have preview images - just clear the preview box
            ClearPreview(pictureBox);
        }

        private void ClearPreview(PictureBox pictureBox)
        {
            pictureBox.Image?.Dispose();
            pictureBox.Image = null;
            pictureBox.BackColor = Color.FromArgb(45, 45, 45);
        }
    }
}