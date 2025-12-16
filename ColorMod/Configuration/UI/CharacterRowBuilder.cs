using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FFTColorMod.Utilities;
using FFTColorMod.Services;

namespace FFTColorMod.Configuration.UI
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

            // Create combo box
            var comboBox = ConfigUIComponentFactory.CreateThemeComboBox();
            // Get available themes from the job class service
            var themes = GetAvailableGenericThemes();
            comboBox.DataSource = themes;
            comboBox.SelectedItem = currentTheme;

            // Create preview picture box
            var pictureBox = ConfigUIComponentFactory.CreatePreviewPictureBox();
            UpdateGenericPreviewImage(pictureBox, jobName, currentTheme);
            pictureBox.Tag = new { JobName = jobName, Setter = setter };

            // Add controls to panel
            _mainPanel.Controls.Add(comboBox, 1, row);
            _mainPanel.Controls.Add(pictureBox, 2, row);

            // Track controls
            _genericCharacterControls.Add(comboBox);
            _genericCharacterControls.Add(pictureBox);

            // Store reference for verification
            comboBox.Tag = new { JobName = jobName, ExpectedValue = currentTheme, Setter = setter };

            // Setup event handler
            comboBox.SelectedIndexChanged += (s, e) =>
            {
                if (_isInitializing != null && _isInitializing()) return;

                // Check if form is fully loaded using the provided function
                if (isFullyLoaded != null && isFullyLoaded() && comboBox.SelectedItem != null)
                {
                    var newTheme = (string)comboBox.SelectedItem;
                    ModLogger.Log($"Selection changed: {jobName} = {newTheme}");
                    setter(newTheme);
                    UpdateGenericPreviewImage(pictureBox, jobName, newTheme);
                }
            };
        }

        public void AddStoryCharacterRow(int row, StoryCharacterRegistry.StoryCharacterConfig characterConfig)
        {
            // Create label
            var label = ConfigUIComponentFactory.CreateCharacterLabel(characterConfig.Name);
            _mainPanel.Controls.Add(label, 0, row);
            _storyCharacterControls.Add(label);

            // Create combo box
            var comboBox = ConfigUIComponentFactory.CreateThemeComboBox();
            var pictureBox = ConfigUIComponentFactory.CreatePreviewPictureBox();

            // Get available themes for this story character
            var availableThemes = characterConfig.AvailableThemes?.Length > 0
                ? characterConfig.AvailableThemes
                : new[] { "original" }; // fallback if no themes available

            Console.WriteLine($"[FFT Color Mod] Story character {characterConfig.Name} has {availableThemes.Length} themes: {string.Join(", ", availableThemes)}");
            comboBox.DataSource = availableThemes;

            // Setup event handler
            comboBox.SelectedIndexChanged += (s, e) =>
            {
                if (_isInitializing != null && _isInitializing()) return;

                // Also check if form is loaded for story characters
                // We need to get the form's _isFullyLoaded state
                // For now, we'll check if the control's handle is created
                if (comboBox.IsHandleCreated && comboBox.SelectedItem != null)
                {
                    characterConfig.SetValue(comboBox.SelectedItem);
                    UpdateStoryCharacterPreview(pictureBox, characterConfig.PreviewName, comboBox.SelectedItem.ToString());
                }
            };

            // Set initial preview
            var currentValue = characterConfig.GetValue();
            UpdateStoryCharacterPreview(pictureBox, characterConfig.PreviewName, currentValue.ToString());
            pictureBox.Tag = new { JobName = characterConfig.PreviewName };

            // Add to panel
            _mainPanel.Controls.Add(comboBox, 1, row);
            _mainPanel.Controls.Add(pictureBox, 2, row);

            // Force Handle creation
            var handle = comboBox.Handle;

            // Refresh if needed
            if (comboBox.Items.Count == 0)
            {
                comboBox.DataSource = null;
                comboBox.DataSource = availableThemes;
                handle = comboBox.Handle;
            }

            // Set selection
            for (int i = 0; i < availableThemes.Length; i++)
            {
                if (availableThemes[i] == currentValue.ToString())
                {
                    comboBox.SelectedIndex = i;
                    break;
                }
            }

            // Track controls
            _storyCharacterControls.Add(comboBox);
            _storyCharacterControls.Add(pictureBox);
        }

        private List<string> GetAvailableGenericThemes()
        {
            // Get themes from the JobClassDefinitionService
            // This could later be loaded from a JSON file for easier configuration
            var themes = _jobClassService.GetAvailableThemes();
            Console.WriteLine($"[FFT Color Mod] GetAvailableGenericThemes returned {themes.Count} themes: {string.Join(", ", themes)}");
            return themes;
        }

        private void UpdateGenericPreviewImage(PictureBox pictureBox, string jobName, string theme)
        {
            string fileName = jobName.ToLower()
                .Replace(" (male)", "_male")
                .Replace(" (female)", "_female")
                .Replace(" ", "_")
                .Replace("(", "")
                .Replace(")", "");

            string resourceName = $"FFTColorMod.Resources.Previews.{fileName}_{theme.ToLower()}.png";

            ModLogger.LogDebug($"Looking for embedded resource: {resourceName}");

            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        pictureBox.Image?.Dispose();
                        pictureBox.Image = Image.FromStream(stream);
                        ModLogger.LogSuccess($"Successfully loaded embedded preview image");
                    }
                    else
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
                                    pictureBox.Image?.Dispose();
                                    pictureBox.Image = Image.FromStream(altStream);
                                    ModLogger.Log($"Loaded with alt resource name: {matchingResource}");
                                }
                            }
                        }
                        else
                        {
                            pictureBox.Image?.Dispose();
                            pictureBox.Image = null;
                            ModLogger.Log($"Embedded resource not found");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"loading embedded preview: {ex.Message}");
                pictureBox.Image?.Dispose();
                pictureBox.Image = null;
            }
        }

        private void UpdateStoryCharacterPreview(PictureBox pictureBox, string characterName, string theme)
        {
            _previewManager?.UpdateStoryCharacterPreview(pictureBox, characterName, theme);
        }
    }
}