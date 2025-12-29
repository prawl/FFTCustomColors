using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace FFTColorCustomizer.ThemeEditor
{
    /// <summary>
    /// Panel containing the Theme Editor UI for creating custom color themes.
    /// </summary>
    public class ThemeEditorPanel : Panel
    {
        private Label _templateLabel;
        private ComboBox _templateDropdown;
        private Button _importClipboardButton;
        private PictureBox _spritePreview;
        private Label _themeNameLabel;
        private TextBox _themeNameInput;
        private Button _saveButton;
        private Button _resetButton;
        private Button _cancelButton;
        private Button _rotateLeftButton;
        private Button _rotateRightButton;
        private Panel _sectionColorPickersPanel;
        private Label _warningLabel;
        private string? _mappingsDirectory;
        private string? _spritesDirectory;
        private Dictionary<string, string> _displayNameToJobName = new();

        public SectionMapping? CurrentMapping { get; private set; }
        public int CurrentSpriteDirection { get; private set; } = 5; // Default to SW (Southwest)
        public PaletteModifier? PaletteModifier { get; private set; }

        public ThemeEditorPanel() : this(null, null)
        {
        }

        public ThemeEditorPanel(string? mappingsDirectory) : this(mappingsDirectory, null)
        {
        }

        public ThemeEditorPanel(string? mappingsDirectory, string? spritesDirectory)
        {
            _mappingsDirectory = mappingsDirectory;
            _spritesDirectory = spritesDirectory;
            MinimumSize = new System.Drawing.Size(0, 460);
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // Layout constants
            const int padding = 10;
            const int dropdownTop = 10;
            const int previewTop = 50; // Increased to add padding below dropdown row
            const int previewWidth = 192;  // 6x scale (32 * 6)
            const int previewHeight = 240; // 6x scale (40 * 6)
            const int colorPickersLeft = 220;
            const int colorPickersHeight = previewHeight; // Match preview height
            const int labelWidth = 65;

            _templateLabel = new Label
            {
                Name = "TemplateLabel",
                Text = "Template:",
                Left = padding,
                Top = dropdownTop + 3,
                AutoSize = true
            };

            _templateDropdown = new ComboBox
            {
                Name = "TemplateDropdown",
                Width = 200,
                Left = padding + labelWidth,
                Top = dropdownTop,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _templateDropdown.SelectedIndexChanged += OnTemplateSelected;

            if (_mappingsDirectory != null)
            {
                var availableJobs = SectionMappingLoader.GetAvailableJobs(_mappingsDirectory);
                foreach (var job in availableJobs)
                {
                    var displayName = JobNameToDisplayName(job);
                    _displayNameToJobName[displayName] = job;
                    _templateDropdown.Items.Add(displayName);
                }
            }

            _importClipboardButton = new Button
            {
                Name = "ImportClipboardButton",
                Text = "Import from Clipboard",
                Left = padding + labelWidth + 210,
                Top = dropdownTop,
                AutoSize = true
            };

            _spritePreview = new PictureBox
            {
                Name = "SpritePreview",
                Width = previewWidth,
                Height = previewHeight,
                Left = padding,
                Top = previewTop,
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom
            };

            _sectionColorPickersPanel = new Panel
            {
                Name = "SectionColorPickersPanel",
                AutoScroll = true,
                Left = colorPickersLeft,
                Top = previewTop,
                Height = colorPickersHeight,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Center rotation buttons under the preview
            const int buttonWidth = 30;
            const int buttonGap = 5;
            var previewCenter = padding + (previewWidth / 2);
            var buttonsLeftStart = previewCenter - buttonWidth - (buttonGap / 2);

            _rotateLeftButton = new Button
            {
                Name = "RotateLeftButton",
                Text = "◄",
                Width = buttonWidth,
                Left = buttonsLeftStart,
                Top = previewTop + previewHeight + 5
            };
            _rotateLeftButton.Click += OnRotateLeft;

            _rotateRightButton = new Button
            {
                Name = "RotateRightButton",
                Text = "►",
                Width = buttonWidth,
                Left = buttonsLeftStart + buttonWidth + buttonGap,
                Top = previewTop + previewHeight + 5
            };
            _rotateRightButton.Click += OnRotateRight;

            // Position bottom controls from the bottom of the panel
            // Warning label at bottom, buttons above it, theme name above buttons
            const int bottomPadding = 10;
            const int warningLabelHeight = 20;
            const int buttonHeight = 25;
            const int themeNameHeight = 25;

            _warningLabel = new Label
            {
                Name = "WarningLabel",
                Text = "⚠ Once saved, themes cannot be edited. Export to modify.",
                Left = padding,
                AutoSize = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            _saveButton = new Button
            {
                Name = "SaveButton",
                Text = "Save Theme",
                Width = 80,
                Left = padding,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            _resetButton = new Button
            {
                Name = "ResetButton",
                Text = "Reset",
                Width = 60,
                Left = padding + 85,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            _cancelButton = new Button
            {
                Name = "CancelButton",
                Text = "Cancel",
                Width = 60,
                Left = padding + 150,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            _themeNameLabel = new Label
            {
                Name = "ThemeNameLabel",
                Text = "Theme Name:",
                Left = padding,
                AutoSize = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            _themeNameInput = new TextBox
            {
                Name = "ThemeNameInput",
                Width = 200,
                Left = padding + 85,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            Controls.Add(_templateLabel);
            Controls.Add(_templateDropdown);
            Controls.Add(_importClipboardButton);
            Controls.Add(_spritePreview);
            Controls.Add(_rotateLeftButton);
            Controls.Add(_rotateRightButton);
            Controls.Add(_themeNameLabel);
            Controls.Add(_themeNameInput);
            Controls.Add(_saveButton);
            Controls.Add(_resetButton);
            Controls.Add(_cancelButton);
            Controls.Add(_warningLabel);
            Controls.Add(_sectionColorPickersPanel);

            // Set initial width for color pickers panel (will be updated on resize)
            UpdateColorPickersPanelWidth();
            PositionBottomControls();

            // Default to Squire Male if available (must be after all controls are initialized)
            var squireMaleIndex = _templateDropdown.Items.IndexOf("Squire Male");
            if (squireMaleIndex >= 0)
            {
                _templateDropdown.SelectedIndex = squireMaleIndex;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateColorPickersPanelWidth();
            PositionBottomControls();
        }

        private void PositionBottomControls()
        {
            // Guard against being called before controls are initialized
            if (_warningLabel == null || _saveButton == null)
                return;

            // Position bottom controls at fixed location below the rotate buttons
            // previewTop(50) + previewHeight(240) + rotateButtons(25) + padding(50) = 365
            const int bottomControlsTop = 365;

            _themeNameLabel.Top = bottomControlsTop;
            _themeNameInput.Top = bottomControlsTop - 3;
            _saveButton.Top = bottomControlsTop + 30;
            _resetButton.Top = _saveButton.Top;
            _cancelButton.Top = _saveButton.Top;
            _warningLabel.Top = _saveButton.Top + 30;
        }

        private void UpdateColorPickersPanelWidth()
        {
            const int colorPickersLeft = 220;
            const int padding = 10;
            var newWidth = Width - colorPickersLeft - padding;
            if (newWidth > 0)
            {
                _sectionColorPickersPanel.Width = newWidth;
            }
        }

        private static string JobNameToDisplayName(string jobName)
        {
            return jobName.Replace("_", " ");
        }

        private void OnTemplateSelected(object? sender, System.EventArgs e)
        {
            if (_mappingsDirectory == null || _templateDropdown.SelectedItem == null)
                return;

            var displayName = _templateDropdown.SelectedItem.ToString();
            if (displayName == null || !_displayNameToJobName.TryGetValue(displayName, out var selectedJob))
                return;

            var mappingPath = Path.Combine(_mappingsDirectory, $"{selectedJob}.json");

            if (File.Exists(mappingPath))
            {
                CurrentMapping = SectionMappingLoader.LoadFromFile(mappingPath);

                // Load sprite into PaletteModifier if sprites directory is set
                if (_spritesDirectory != null && CurrentMapping != null)
                {
                    var spritePath = Path.Combine(_spritesDirectory, CurrentMapping.Sprite);
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

                var picker = new HslColorPicker
                {
                    SectionName = section.DisplayName,
                    Dock = DockStyle.Top
                };

                // Initialize picker with base color from palette (before subscribing to events)
                if (PaletteModifier != null && PaletteModifier.IsLoaded)
                {
                    var baseIndex = GetBaseIndexForSection(section);
                    var baseColor = PaletteModifier.GetPaletteColor(baseIndex);
                    picker.SetColorSilent(baseColor);
                }

                picker.ColorChanged += OnColorPickerChanged;
                _sectionColorPickersPanel.Controls.Add(picker);
            }
        }

        private int GetBaseIndexForSection(JobSection section)
        {
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
            var currentIndex = Array.IndexOf(DirectionsCycle, CurrentSpriteDirection);
            if (currentIndex == -1) currentIndex = 0; // Default to first if not found
            currentIndex = (currentIndex + DirectionsCycle.Length - 1) % DirectionsCycle.Length;
            CurrentSpriteDirection = DirectionsCycle[currentIndex];
            UpdateSpritePreview();
        }

        private void OnRotateRight(object? sender, System.EventArgs e)
        {
            var currentIndex = Array.IndexOf(DirectionsCycle, CurrentSpriteDirection);
            if (currentIndex == -1) currentIndex = 0; // Default to first if not found
            currentIndex = (currentIndex + 1) % DirectionsCycle.Length;
            CurrentSpriteDirection = DirectionsCycle[currentIndex];
            UpdateSpritePreview();
        }
    }
}
