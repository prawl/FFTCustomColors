using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FFTColorCustomizer.Utilities;
using FFTColorCustomizer.Services;

namespace FFTColorCustomizer.Configuration.UI
{
    /// <summary>
    /// Builds character rows for the configuration form
    /// </summary>
    public class CharacterRowBuilder
    {
        private readonly TableLayoutPanel _mainPanel;
        private readonly PreviewImageManager _previewManager;
        private readonly Func<bool> _isInitializing;
        private readonly List<Control> _genericCharacterControls;
        private readonly List<Control> _storyCharacterControls;
        private readonly JobClassDefinitionService _jobClassService;

        public CharacterRowBuilder(
            TableLayoutPanel mainPanel,
            PreviewImageManager previewManager,
            Func<bool> isInitializing,
            List<Control> genericCharacterControls,
            List<Control> storyCharacterControls)
        {
            _mainPanel = mainPanel;
            _previewManager = previewManager;
            _isInitializing = isInitializing;
            _genericCharacterControls = genericCharacterControls;
            _storyCharacterControls = storyCharacterControls;
            _jobClassService = JobClassServiceSingleton.Instance;
        }

        public void AddGenericCharacterRow(int row, string jobName, string currentTheme,
            Action<string> setter, Func<bool> isFullyLoaded)
        {
            ModLogger.Log($"AddJobRow: {jobName} = {currentTheme}");

            // Create label
            var label = ConfigUIComponentFactory.CreateCharacterLabel(jobName);
            _mainPanel.Controls.Add(label, 0, row);
            _genericCharacterControls.Add(label);

            // Create theme combo box with formatted display
            var comboBox = new ThemeComboBox();
            ConfigUIComponentFactory.ApplyThemeComboBoxStyling(comboBox);
            // Get available themes from the job class service
            var themes = GetAvailableGenericThemes();
            comboBox.SetThemes(themes);
            comboBox.SelectedThemeValue = currentTheme;

            // Create preview carousel
            var carousel = ConfigUIComponentFactory.CreatePreviewPictureBox();
            UpdateGenericPreviewImages(carousel, jobName, currentTheme);
            carousel.Tag = new { JobName = jobName, Setter = setter };

            // Add controls to panel
            _mainPanel.Controls.Add(comboBox, 1, row);
            _mainPanel.Controls.Add(carousel, 2, row);

            // Track controls
            _genericCharacterControls.Add(comboBox);
            _genericCharacterControls.Add(carousel);

            // Store reference for verification
            comboBox.Tag = new { JobName = jobName, ExpectedValue = currentTheme, Setter = setter };

            // Setup event handler
            comboBox.SelectedThemeChanged += (s, newTheme) =>
            {
                if (_isInitializing != null && _isInitializing()) return;

                // Check if form is fully loaded using the provided function
                if (isFullyLoaded != null && isFullyLoaded() && !string.IsNullOrEmpty(newTheme))
                {
                    ModLogger.Log($"Selection changed: {jobName} = {newTheme}");
                    setter(newTheme);
                    UpdateGenericPreviewImages(carousel, jobName, newTheme);
                }
            };
        }

        public void AddStoryCharacterRow(int row, StoryCharacterRegistry.StoryCharacterConfig characterConfig)
        {
            // Create label
            var label = ConfigUIComponentFactory.CreateCharacterLabel(characterConfig.Name);
            _mainPanel.Controls.Add(label, 0, row);
            _storyCharacterControls.Add(label);

            // Create theme combo box with formatted display
            var comboBox = new ThemeComboBox();
            ConfigUIComponentFactory.ApplyThemeComboBoxStyling(comboBox);
            var carousel = ConfigUIComponentFactory.CreatePreviewPictureBox();

            // Get available themes for this story character
            var availableThemes = characterConfig.AvailableThemes?.Length > 0
                ? characterConfig.AvailableThemes
                : new[] { "original" }; // fallback if no themes available

            ModLogger.LogDebug($"Story character {characterConfig.Name} has {availableThemes.Length} themes: {string.Join(", ", availableThemes)}");
            comboBox.SetThemes(availableThemes);

            // Setup event handler
            comboBox.SelectedThemeChanged += (s, newTheme) =>
            {
                if (_isInitializing != null && _isInitializing()) return;

                // Also check if form is loaded for story characters
                // We need to get the form's _isFullyLoaded state
                // For now, we'll check if the control's handle is created
                if (comboBox.IsHandleCreated && !string.IsNullOrEmpty(newTheme))
                {
                    characterConfig.SetValue(newTheme);
                    UpdateStoryCharacterPreview(carousel, characterConfig.PreviewName, newTheme);
                }
            };

            // Set initial preview
            var currentValue = characterConfig.GetValue();
            UpdateStoryCharacterPreview(carousel, characterConfig.PreviewName, currentValue.ToString());
            carousel.Tag = new { JobName = characterConfig.PreviewName };

            // Add to panel
            _mainPanel.Controls.Add(comboBox, 1, row);
            _mainPanel.Controls.Add(carousel, 2, row);

            // Force Handle creation
            var handle = comboBox.Handle;

            // Set the initial selection using the internal value
            comboBox.SelectedThemeValue = currentValue.ToString();

            // Track controls
            _storyCharacterControls.Add(comboBox);
            _storyCharacterControls.Add(carousel);
        }

        private List<string> GetAvailableGenericThemes()
        {
            // Get themes from the JobClassDefinitionService
            // This could later be loaded from a JSON file for easier configuration
            var themes = _jobClassService.GetAvailableThemes();
            ModLogger.Log($"GetAvailableGenericThemes returned {themes.Count} themes: {string.Join(", ", themes)}");
            return themes;
        }

        private void UpdateGenericPreviewImages(PreviewCarousel carousel, string jobName, string theme)
        {
            string fileName = jobName.ToLower()
                .Replace(" (male)", "_male")
                .Replace(" (female)", "_female")
                .Replace(" ", "_")
                .Replace("(", "")
                .Replace(")", "");

            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var images = new List<Image>();

            // First try to load 4 corner sprites from embedded resources
            string[] corners = { "_sw", "_se", "_ne", "_nw" };  // SW default, then clockwise

            foreach (var corner in corners)
            {
                // Resource name format: FFTColorCustomizer.Resources.Previews.{job_folder}.{job}_{theme}_{corner}.png
                string resourceName = $"FFTColorCustomizer.Resources.Previews.{fileName}.{fileName}_{theme.ToLower()}{corner}.png";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        try
                        {
                            var image = Image.FromStream(stream);
                            images.Add(image);
                            ModLogger.LogDebug($"Loaded corner sprite: {corner} for {fileName}_{theme}");
                        }
                        catch (Exception ex)
                        {
                            ModLogger.LogDebug($"Failed to load corner {corner}: {ex.Message}");
                        }
                    }
                }
            }

            // If we loaded corner sprites, use them
            if (images.Count > 0)
            {
                carousel.SetImages(images.ToArray());
                ModLogger.LogSuccess($"Loaded {images.Count} corner views for {jobName} - {theme}");
                return;
            }

            // Fall back to original single-image loading
            foreach (var direction in new[] { "" })
            {
                string resourceName = $"FFTColorCustomizer.Resources.Previews.{fileName}_{theme.ToLower()}{direction}.png";
                ModLogger.LogDebug($"Looking for directional resource: {resourceName}");

                // Also log the actual resource name we're looking for
                System.Diagnostics.Debug.WriteLine($"[CAROUSEL] Trying to load: {resourceName}");

                try
                {
                    using (var stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            var image = Image.FromStream(stream);
                            images.Add(image);
                            ModLogger.LogDebug($"Loaded directional view: {direction}");
                            System.Diagnostics.Debug.WriteLine($"[CAROUSEL] SUCCESS: Loaded {resourceName}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[CAROUSEL] FAILED: Stream null for {resourceName}");

                            if (direction == "") // Only check alternate names for main image
                            {
                                var allResources = assembly.GetManifestResourceNames();
                                var matchingResource = allResources.FirstOrDefault(r =>
                                    r.EndsWith($"{fileName}_{theme.ToString().ToLower()}.png", StringComparison.OrdinalIgnoreCase));

                                if (!string.IsNullOrEmpty(matchingResource))
                                {
                                    using (var altStream = assembly.GetManifestResourceStream(matchingResource))
                                    {
                                        if (altStream != null)
                                        {
                                            images.Add(Image.FromStream(altStream));
                                            ModLogger.LogDebug($"Loaded main view with alt resource name");
                                            System.Diagnostics.Debug.WriteLine($"[CAROUSEL] SUCCESS via alt: {matchingResource}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.LogDebug($"Could not load direction {direction}: {ex.Message}");
                }
            }

            // Set the images in the carousel
            if (images.Count > 0)
            {
                carousel.SetImages(images.ToArray());
                ModLogger.LogSuccess($"Loaded {images.Count} directional views for {jobName} - {theme}");
            }
            else
            {
                carousel.SetImages(new Image[0]);
                ModLogger.Log($"No preview images found for {jobName} - {theme}");
            }
        }

        private void UpdateStoryCharacterPreview(PreviewCarousel carousel, string characterName, string theme)
        {
            var images = new List<Image>();

            // Try to load multiple directional views for story characters
            if (_previewManager != null)
            {
                // Directional suffixes to try (empty string for main view)
                string[] directions = { "", "_n", "_ne", "_e", "_se", "_s", "_sw", "_w", "_nw" };

                foreach (var direction in directions)
                {
                    // For story characters, we'll try to load directional views if they exist
                    // This is a placeholder for now - when directional sprites are available,
                    // the PreviewImageManager will need to be updated to support them
                    using (var tempPictureBox = new PictureBox())
                    {
                        _previewManager.UpdateStoryCharacterPreview(tempPictureBox, characterName, theme);
                        if (tempPictureBox.Image != null && direction == "")
                        {
                            // For now, just add the main image
                            // When directional sprites are available, we'll load them properly
                            images.Add(tempPictureBox.Image);
                            break; // Only use main image for now
                        }
                    }
                }
            }

            // Set the images in the carousel
            if (images.Count > 0)
            {
                carousel.SetImages(images.ToArray());
                ModLogger.LogDebug($"Loaded {images.Count} view(s) for story character {characterName} - {theme}");
            }
            else
            {
                carousel.SetImages(new Image[0]);
                ModLogger.LogDebug($"No preview images found for story character {characterName} - {theme}");
            }
        }
    }
}
