using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Services;

namespace FFTColorCustomizer.ThemeEditor
{
    /// <summary>
    /// Panel containing the Theme Editor UI for creating custom color themes.
    /// </summary>
    public class ThemeEditorPanel : Panel
    {
        private Label _templateLabel;
        private ComboBox _templateDropdown;
        private Label _spritePreviewLabel;
        private Label _colorSelectionLabel;
        private PictureBox _spritePreview;
        private Label _themeNameLabel;
        private TextBox _themeNameInput;
        private Button _saveButton;
        private Button _resetButton;
        private Button _rotateLeftButton;
        private Button _rotateRightButton;
        private Panel _sectionColorPickersPanel;
        private Label _warningLabel;
        private string? _mappingsDirectory;
        private string? _spritesDirectory;
        private string? _modsDirectory;
        private Dictionary<string, string> _displayNameToJobName = new();
        private Dictionary<string, bool> _isStoryCharacter = new();
        private Dictionary<string, bool> _isWotLCharacter = new();
        private bool _suppressColorChangedEvents;

        public SectionMapping? CurrentMapping { get; private set; }
        public int CurrentSpriteDirection { get; private set; } = 5; // Default to SW (Southwest)
        public PaletteModifier? PaletteModifier { get; private set; }
        public bool HasUnsavedChanges { get; private set; }

        public event EventHandler? ThemeSaved;

        public void MarkAsModified()
        {
            HasUnsavedChanges = true;
        }

        public void ClearModified()
        {
            HasUnsavedChanges = false;
        }

        public ThemeEditorPanel() : this(null, null, null)
        {
        }

        public ThemeEditorPanel(string? mappingsDirectory) : this(mappingsDirectory, null, null)
        {
        }

        public ThemeEditorPanel(string? mappingsDirectory, string? spritesDirectory) : this(mappingsDirectory, spritesDirectory, null)
        {
        }

        public ThemeEditorPanel(string? mappingsDirectory, string? spritesDirectory, string? modsDirectory)
        {
            _mappingsDirectory = mappingsDirectory;
            _spritesDirectory = spritesDirectory;
            _modsDirectory = modsDirectory;
            MinimumSize = new System.Drawing.Size(0, 550);
            Height = 550; // Set initial height so panel sizing works
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // Layout constants
            const int padding = 10;
            const int row1Top = 10;
            const int previewWidth = 192;  // 6x scale (32 * 6)
            const int previewHeight = 240; // 6x scale (40 * 6)
            const int colorPickersLeft = 220;
            const int templateLabelWidth = 85;  // Width for "Template:" label
            const int themeNameLabelWidth = 105; // Width for "Theme Name:" label

            // === ROW 1: Template dropdown + Theme Name + Buttons ===
            _templateLabel = new Label
            {
                Name = "TemplateLabel",
                Text = "Template:",
                Left = padding,
                Top = row1Top + 3,
                AutoSize = true
            };

            _templateDropdown = new ComboBox
            {
                Name = "TemplateDropdown",
                Width = 140,
                Left = padding + templateLabelWidth,
                Top = row1Top,
                DropDownStyle = ComboBoxStyle.DropDownList,
                MaxDropDownItems = 20
            };
            _templateDropdown.SelectedIndexChanged += OnTemplateSelected;
            _templateDropdown.MouseWheel += (s, e) => ((HandledMouseEventArgs)e).Handled = true; // Disable mouse wheel scroll

            if (_mappingsDirectory != null)
            {
                // WotL jobs to exclude from generic jobs (they're listed separately)
                var wotlJobs = new HashSet<string> { "DarkKnight_Male", "DarkKnight_Female", "OnionKnight_Male", "OnionKnight_Female" };

                // Add generic jobs
                var availableJobs = SectionMappingLoader.GetAvailableJobs(_mappingsDirectory);
                foreach (var job in availableJobs)
                {
                    // Skip WotL jobs - they're listed in their own section
                    if (wotlJobs.Contains(job))
                        continue;

                    var displayName = JobNameToDisplayName(job);
                    _displayNameToJobName[displayName] = job;
                    _isStoryCharacter[displayName] = false;
                    _templateDropdown.Items.Add(displayName);
                }
            }

            // Add Story Characters section
            _templateDropdown.Items.Add("── Story Characters ──");

            if (_mappingsDirectory != null)
            {
                // Add story characters from Story/ subdirectory (includes Ramza chapters)
                var storyCharacters = SectionMappingLoader.GetAvailableStoryCharacters(_mappingsDirectory);
                foreach (var character in storyCharacters)
                {
                    // Map Ramza chapter job names to friendlier display names
                    var displayName = character switch
                    {
                        "RamzaCh1" => "Ramza (Chapter 1)",
                        "RamzaCh23" => "Ramza (Chapter 2/3)",
                        "RamzaCh4" => "Ramza (Chapter 4)",
                        _ => character // Other story characters use their name as-is
                    };
                    _displayNameToJobName[displayName] = character;
                    _isStoryCharacter[displayName] = true;
                    _templateDropdown.Items.Add(displayName);
                }

                // Add WotL Characters section
                _templateDropdown.Items.Add("── WotL Characters ──");

                // Add Dark Knight (Male/Female)
                _displayNameToJobName["Dark Knight (Male)"] = "DarkKnight_Male";
                _isStoryCharacter["Dark Knight (Male)"] = false;
                _isWotLCharacter["Dark Knight (Male)"] = true;
                _templateDropdown.Items.Add("Dark Knight (Male)");

                _displayNameToJobName["Dark Knight (Female)"] = "DarkKnight_Female";
                _isStoryCharacter["Dark Knight (Female)"] = false;
                _isWotLCharacter["Dark Knight (Female)"] = true;
                _templateDropdown.Items.Add("Dark Knight (Female)");

                // Add Onion Knight (Male/Female)
                _displayNameToJobName["Onion Knight (Male)"] = "OnionKnight_Male";
                _isStoryCharacter["Onion Knight (Male)"] = false;
                _isWotLCharacter["Onion Knight (Male)"] = true;
                _templateDropdown.Items.Add("Onion Knight (Male)");

                _displayNameToJobName["Onion Knight (Female)"] = "OnionKnight_Female";
                _isStoryCharacter["Onion Knight (Female)"] = false;
                _isWotLCharacter["Onion Knight (Female)"] = true;
                _templateDropdown.Items.Add("Onion Knight (Female)");
            }

            // Theme name input (to the right of template dropdown)
            var themeNameLeft = padding + templateLabelWidth + 150;
            _themeNameLabel = new Label
            {
                Name = "ThemeNameLabel",
                Text = "Theme Name:",
                Left = themeNameLeft,
                Top = row1Top + 3,
                AutoSize = true
            };

            _themeNameInput = new TextBox
            {
                Name = "ThemeNameInput",
                Width = 120,
                Left = themeNameLeft + themeNameLabelWidth,
                Top = row1Top
            };

            // Buttons (to the right of theme name)
            var buttonsLeft = themeNameLeft + themeNameLabelWidth + 130;
            _saveButton = new Button
            {
                Name = "SaveButton",
                Text = "Save",
                Width = 50,
                Left = buttonsLeft,
                Top = row1Top
            };
            _saveButton.Click += OnSaveClick;

            _resetButton = new Button
            {
                Name = "ResetButton",
                Text = "Reset All",
                Width = 70,
                Left = buttonsLeft + 55,
                Top = row1Top,
                FlatStyle = FlatStyle.Flat,
                BackColor = UIConfiguration.ResetButtonColor,
                ForeColor = Color.White
            };
            _resetButton.FlatAppearance.BorderColor = UIConfiguration.ResetButtonBorder;
            _resetButton.Click += OnResetAllClick;

            // Warning label (below the Save button, row 2) - positioned to the left to avoid clipping
            const int row2Top = 40;
            _warningLabel = new Label
            {
                Name = "WarningLabel",
                Text = "⚠ Once saved, themes cannot be edited.",
                Left = themeNameLeft,
                Top = row2Top,
                AutoSize = true,
                ForeColor = Color.FromArgb(255, 200, 50)
            };

            // === ROW 3: Section labels above the content panels ===
            const int row3Top = 65;
            const int labelHeight = 20;
            const int contentTop = row3Top + labelHeight + 5; // 5px gap between label and content

            _spritePreviewLabel = new Label
            {
                Name = "SpritePreviewLabel",
                Text = "Sprite Preview",
                Left = padding,
                Top = row3Top,
                AutoSize = true
            };

            _colorSelectionLabel = new Label
            {
                Name = "ColorSelectionLabel",
                Text = "Color Customizer",
                Left = colorPickersLeft,
                Top = row3Top,
                AutoSize = true
            };

            // === ROW 4: Sprite preview (left) + Color pickers panel (right) ===
            _spritePreview = new PictureBox
            {
                Name = "SpritePreview",
                Width = previewWidth,
                Height = previewHeight,
                Left = padding,
                Top = contentTop,
                BorderStyle = BorderStyle.None,
                SizeMode = PictureBoxSizeMode.Zoom
            };

            // Center rotation buttons under the preview
            const int buttonWidth = 30;
            const int buttonGap = 5;
            var previewCenter = padding + (previewWidth / 2);
            var rotateButtonsLeftStart = previewCenter - buttonWidth - (buttonGap / 2);

            _rotateLeftButton = new Button
            {
                Name = "RotateLeftButton",
                Text = "◄",
                Width = buttonWidth,
                Left = rotateButtonsLeftStart,
                Top = contentTop + previewHeight + 5
            };
            _rotateLeftButton.Click += OnRotateLeft;

            _rotateRightButton = new Button
            {
                Name = "RotateRightButton",
                Text = "►",
                Width = buttonWidth,
                Left = rotateButtonsLeftStart + buttonWidth + buttonGap,
                Top = contentTop + previewHeight + 5
            };
            _rotateRightButton.Click += OnRotateRight;

            // Color pickers panel - extends to bottom
            _sectionColorPickersPanel = new Panel
            {
                Name = "SectionColorPickersPanel",
                AutoScroll = true,
                Left = colorPickersLeft,
                Top = contentTop,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };

            Controls.Add(_templateLabel);
            Controls.Add(_templateDropdown);
            Controls.Add(_themeNameLabel);
            Controls.Add(_themeNameInput);
            Controls.Add(_saveButton);
            Controls.Add(_resetButton);
            Controls.Add(_warningLabel);
            Controls.Add(_spritePreviewLabel);
            Controls.Add(_colorSelectionLabel);
            Controls.Add(_spritePreview);
            Controls.Add(_rotateLeftButton);
            Controls.Add(_rotateRightButton);
            Controls.Add(_sectionColorPickersPanel);

            // Set initial sizes for dynamic panels
            UpdateColorPickersPanelSize();

            // Default to Squire (Male) if available (must be after all controls are initialized)
            var squireMaleIndex = _templateDropdown.Items.IndexOf("Squire (Male)");
            if (squireMaleIndex >= 0)
            {
                _templateDropdown.SelectedIndex = squireMaleIndex;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateColorPickersPanelSize();
        }

        private void UpdateColorPickersPanelSize()
        {
            // Guard against being called before controls are initialized
            if (_sectionColorPickersPanel == null)
                return;

            const int colorPickersLeft = 220;
            const int padding = 10;
            const int row3Top = 65;
            const int labelHeight = 20;
            const int contentTop = row3Top + labelHeight + 5; // Match the label offset

            // Width: extend to right edge
            var newWidth = Width - colorPickersLeft - padding;
            if (newWidth > 0)
            {
                _sectionColorPickersPanel.Width = newWidth;
            }

            // Height: extend to bottom edge
            var newHeight = Height - contentTop - padding;
            if (newHeight > 0)
            {
                _sectionColorPickersPanel.Height = newHeight;
            }
        }

        private static string JobNameToDisplayName(string jobName)
        {
            if (string.IsNullOrEmpty(jobName))
                return jobName;

            // Split on underscore
            var parts = jobName.Split('_');

            // If there's no underscore, return as-is
            if (parts.Length == 1)
                return jobName;

            // If there are exactly 2 parts (Job_Gender format)
            if (parts.Length == 2)
            {
                var jobClass = parts[0];
                var gender = parts[1];
                return $"{jobClass} ({gender})";
            }

            // Fallback: just replace underscores with spaces
            return jobName.Replace("_", " ");
        }

        private void OnTemplateSelected(object? sender, System.EventArgs e)
        {
            if (_templateDropdown.SelectedItem == null)
                return;

            var displayName = _templateDropdown.SelectedItem.ToString();

            // Ignore separator selections
            if (displayName == "── Story Characters ──" || displayName == "── WotL Characters ──")
                return;

            if (displayName == null || !_displayNameToJobName.TryGetValue(displayName, out var selectedJob))
                return;

            // We need mappings directory for all characters
            if (_mappingsDirectory == null)
                return;

            // Check if this is a WotL character
            var isWotL = _isWotLCharacter.TryGetValue(displayName, out var wotl) && wotl;

            // Determine mapping path based on whether this is a story character
            string mappingPath;
            if (_isStoryCharacter.TryGetValue(displayName, out var isStory) && isStory)
            {
                mappingPath = Path.Combine(_mappingsDirectory, "Story", $"{selectedJob}.json");
            }
            else
            {
                mappingPath = Path.Combine(_mappingsDirectory, $"{selectedJob}.json");
            }

            if (File.Exists(mappingPath))
            {
                CurrentMapping = SectionMappingLoader.LoadFromFile(mappingPath);

                // Load sprite into PaletteModifier if sprites directory is set
                if (_spritesDirectory != null && CurrentMapping != null)
                {
                    // Resolve sprite path based on character type
                    string spritePath;
                    if (isStory)
                    {
                        spritePath = StoryCharacterSpritePathResolver.ResolveSpritePath(
                            _spritesDirectory, selectedJob, CurrentMapping.Sprite);
                    }
                    else if (isWotL)
                    {
                        // WotL jobs use unit_psp directory instead of unit
                        var unitPspPath = _spritesDirectory.Replace("unit", "unit_psp");
                        spritePath = Path.Combine(unitPspPath, "sprites_original", CurrentMapping.Sprite);
                    }
                    else
                    {
                        spritePath = Path.Combine(_spritesDirectory, "sprites_original", CurrentMapping.Sprite);
                    }

                    if (File.Exists(spritePath))
                    {
                        PaletteModifier = new PaletteModifier();
                        PaletteModifier.LoadTemplate(spritePath);
                        UpdateSpritePreview();
                    }
                }

                // Generate color pickers for each section
                GenerateSectionColorPickers();
            }
        }

        private void GenerateSectionColorPickers()
        {
            _sectionColorPickersPanel.Controls.Clear();

            if (CurrentMapping?.Sections == null)
                return;

            // Build list of sections to display (respecting link filtering)
            var sectionsToDisplay = new System.Collections.Generic.List<JobSection>();
            foreach (var section in CurrentMapping.Sections)
            {
                // Skip sections that are the target of another section's link,
                // UNLESS this section also links out (bidirectional - first one wins)
                var linkerSection = Array.Find(CurrentMapping.Sections, s => s.LinkedTo == section.Name && s != section);
                if (linkerSection != null && section.LinkedTo == null)
                    continue;
                // For bidirectional links, skip the second one (the one being linked TO by an earlier section)
                if (linkerSection != null && section.LinkedTo != null)
                {
                    var linkerIndex = Array.IndexOf(CurrentMapping.Sections, linkerSection);
                    var thisIndex = Array.IndexOf(CurrentMapping.Sections, section);
                    if (linkerIndex < thisIndex)
                        continue;
                }
                sectionsToDisplay.Add(section);
            }

            // Add controls in reverse order so DockStyle.Top displays them in JSON order
            for (int i = sectionsToDisplay.Count - 1; i >= 0; i--)
            {
                var section = sectionsToDisplay[i];
                var picker = new HslColorPicker
                {
                    SectionName = section.DisplayName,
                    Dock = DockStyle.Top,
                    Margin = new Padding(0, 0, 0, 15), // Add spacing between sections
                    BorderStyle = BorderStyle.FixedSingle // Add border to separate sections
                };

                // Initialize picker with base color from palette (before subscribing to events)
                if (PaletteModifier != null && PaletteModifier.IsLoaded)
                {
                    var baseIndex = GetBaseIndexForSection(section);
                    var baseColor = PaletteModifier.GetPaletteColor(baseIndex);
                    picker.SetColorSilent(baseColor);
                    picker.StoreOriginalColor(); // Store for reset functionality
                }

                picker.ColorChanged += OnColorPickerChanged;
                picker.ResetRequested += OnColorPickerResetRequested;
                _sectionColorPickersPanel.Controls.Add(picker);
            }
        }

        private int GetBaseIndexForSection(JobSection section)
        {
            // If primaryIndex is explicitly set, use it
            if (section.PrimaryIndex.HasValue)
                return section.PrimaryIndex.Value;

            // Find the index with "base" role, or use the first index if no base role
            for (int i = 0; i < section.Roles.Length; i++)
            {
                if (section.Roles[i] == "base")
                    return section.Indices[i];
            }
            return section.Indices[0];
        }

        private void OnColorPickerChanged(object? sender, EventArgs e)
        {
            if (sender is not HslColorPicker picker || PaletteModifier == null || CurrentMapping == null)
                return;

            // Find the section matching this picker
            var section = Array.Find(CurrentMapping.Sections, s => s.DisplayName == picker.SectionName);
            if (section == null)
                return;

            // Mark as modified when colors change
            MarkAsModified();

            // Apply the color to the palette
            PaletteModifier.ApplySectionColor(section, picker.CurrentColor);

            // Also apply to linked sections
            if (section.LinkedTo != null)
            {
                var linkedSection = Array.Find(CurrentMapping.Sections, s => s.Name == section.LinkedTo);
                if (linkedSection != null)
                {
                    PaletteModifier.ApplySectionColor(linkedSection, picker.CurrentColor);
                }
            }

            // Update the preview
            UpdateSpritePreview();
        }

        private void OnColorPickerResetRequested(object? sender, EventArgs e)
        {
            // When a section's reset button is clicked, restore the original palette colors
            // from the sprite file instead of regenerating shades from the base color
            if (sender is not HslColorPicker picker || CurrentMapping == null || _spritesDirectory == null || PaletteModifier == null)
                return;

            var section = Array.Find(CurrentMapping.Sections, s => s.DisplayName == picker.SectionName);
            if (section == null)
                return;

            var spritePath = GetCurrentSpritePath();
            if (spritePath == null || !File.Exists(spritePath))
                return;

            // Load the original sprite file to get the original palette colors
            var originalPalette = new PaletteModifier();
            originalPalette.LoadTemplate(spritePath);

            // Restore each index in this section from the original palette
            // Use raw byte copy to avoid precision loss from BGR555/RGB conversion
            foreach (var index in section.Indices)
            {
                PaletteModifier.CopyPaletteIndex(index, originalPalette);
            }

            // Update the preview
            UpdateSpritePreview();
        }

        private void UpdateSpritePreview()
        {
            if (PaletteModifier == null || !PaletteModifier.IsLoaded)
                return;

            _spritePreview.Image = PaletteModifier.GetPreview(CurrentSpriteDirection);
        }

        // All 8 directions cycle (clockwise): SW(5) → S(4) → SE(3) → E(2) → NE(1) → N(0) → NW(7) → W(6) → SW(5)
        private static readonly int[] DirectionsCycle = { 5, 4, 3, 2, 1, 0, 7, 6 };

        private void OnRotateLeft(object? sender, System.EventArgs e)
        {
            // Left arrow now rotates clockwise
            var currentIndex = Array.IndexOf(DirectionsCycle, CurrentSpriteDirection);
            if (currentIndex == -1) currentIndex = 0; // Default to first if not found
            currentIndex = (currentIndex + 1) % DirectionsCycle.Length;
            CurrentSpriteDirection = DirectionsCycle[currentIndex];
            UpdateSpritePreview();
        }

        private void OnRotateRight(object? sender, System.EventArgs e)
        {
            // Right arrow now rotates counter-clockwise
            var currentIndex = Array.IndexOf(DirectionsCycle, CurrentSpriteDirection);
            if (currentIndex == -1) currentIndex = 0; // Default to first if not found
            currentIndex = (currentIndex + DirectionsCycle.Length - 1) % DirectionsCycle.Length;
            CurrentSpriteDirection = DirectionsCycle[currentIndex];
            UpdateSpritePreview();
        }

        private void OnSaveClick(object? sender, EventArgs e)
        {
            var themeName = _themeNameInput.Text;

            // Validate theme name is not empty
            if (string.IsNullOrWhiteSpace(themeName))
                return;

            var jobName = GetCurrentJobName();
            var paletteData = PaletteModifier?.GetModifiedPalette() ?? new byte[512];

            var args = new ThemeSavedEventArgs(jobName, themeName, paletteData);
            ThemeSaved?.Invoke(this, args);

            // Clear the modified flag after saving
            ClearModified();
        }

        private void OnResetAllClick(object? sender, EventArgs e)
        {
            // Confirm with user before resetting
            var result = MessageBox.Show(
                "Are you sure you want to reset all colors back to default? This will save immediately.",
                "Reset All Colors",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            // Clear theme name
            _themeNameInput.Text = string.Empty;

            // Reset all color pickers to their original colors
            foreach (Control control in _sectionColorPickersPanel.Controls)
            {
                if (control is HslColorPicker picker)
                {
                    picker.ResetToOriginal();
                }
            }

            // Reload the palette from scratch to reset the preview
            var spritePath = GetCurrentSpritePath();
            if (spritePath != null && File.Exists(spritePath))
            {
                PaletteModifier = new PaletteModifier();
                PaletteModifier.LoadTemplate(spritePath);
                UpdateSpritePreview();
            }
        }

        private string GetCurrentJobName()
        {
            var displayName = _templateDropdown.SelectedItem?.ToString();
            if (displayName != null && _displayNameToJobName.TryGetValue(displayName, out var jobName))
            {
                return jobName;
            }
            return string.Empty;
        }

        private string? GetCurrentSpritePath()
        {
            if (_spritesDirectory == null || CurrentMapping == null)
                return null;

            var displayName = _templateDropdown.SelectedItem?.ToString();
            if (displayName == null)
                return null;

            // Use resolver for story characters (handles fallback), simple path for generic jobs
            if (_isStoryCharacter.TryGetValue(displayName, out var isStory) && isStory)
            {
                var jobName = GetCurrentJobName();
                return StoryCharacterSpritePathResolver.ResolveSpritePath(
                    _spritesDirectory, jobName, CurrentMapping.Sprite);
            }

            // WotL jobs use unit_psp directory instead of unit
            if (_isWotLCharacter.TryGetValue(displayName, out var isWotL) && isWotL)
            {
                var unitPspPath = _spritesDirectory.Replace("unit", "unit_psp");
                return StoryCharacterSpritePathResolver.ResolveWotLSpritePath(
                    unitPspPath, CurrentMapping.Sprite);
            }

            return Path.Combine(_spritesDirectory, "sprites_original", CurrentMapping.Sprite);
        }
    }
}
