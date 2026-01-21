using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using FFTColorCustomizer.Configuration.UI;

namespace FFTColorCustomizer.ThemeEditor
{
    /// <summary>
    /// A color picker component using HSL (Hue, Saturation, Lightness) sliders.
    /// </summary>
    public class HslColorPicker : Panel
    {
        private NoScrollTrackBar _hueSlider;
        private NoScrollTrackBar _saturationSlider;
        private NoScrollTrackBar _lightnessSlider;
        private Label _sectionHeaderLabel;
        private Button _resetButton;
        private Panel _colorPreviewSwatch;
        private TextBox _hexInput;
        private Button _copyButton;
        private Button _pasteButton;
        private CheckBox _lockCheckbox;
        private bool _suppressEvents;
        private int _originalHue;
        private int _originalSaturation;
        private int _originalLightness;

        /// <summary>
        /// Gets or sets whether this section is locked from randomization.
        /// </summary>
        public bool IsLocked
        {
            get => _lockCheckbox?.Checked ?? false;
            set
            {
                if (_lockCheckbox != null)
                    _lockCheckbox.Checked = value;
            }
        }

        public event EventHandler ColorChanged;
        public event EventHandler ResetRequested;

        private string _sectionName;
        public string SectionName
        {
            get => _sectionName;
            set
            {
                _sectionName = value;
                if (_sectionHeaderLabel != null)
                    _sectionHeaderLabel.Text = value;
            }
        }

        public HslColorPicker()
        {
            // Add bottom margin for visual separation between sections
            Margin = new Padding(0, 0, 0, 35);
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            const int headerHeight = 50; // Increased to accommodate larger section label
            const int rowHeight = 45;
            const int labelWidth = 70;
            const int padding = 10;

            // Section header label - larger, bold, white, centered for visibility
            _sectionHeaderLabel = new Label
            {
                Name = "SectionHeaderLabel",
                Text = _sectionName ?? "",
                Top = 10,
                Left = 0,
                Height = 25,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new System.Drawing.Font("Segoe UI", 13f, System.Drawing.FontStyle.Bold),
                ForeColor = Color.White,
                Padding = new Padding(0, 5, 0, 5)
            };

            // Lock checkbox - positioned below header, excludes section from randomization
            _lockCheckbox = new CheckBox
            {
                Name = "LockCheckbox",
                Text = "Skip Randomize",
                Top = 30,
                Left = 5,
                Width = 100,
                Height = 20,
                ForeColor = Color.LightGray,
                Font = new System.Drawing.Font("Segoe UI", 8f),
                Cursor = Cursors.Hand
            };
            var lockTip = new ToolTip();
            lockTip.SetToolTip(_lockCheckbox, "When checked, this section keeps its colors during Randomize");

            // Reset button - positioned below sliders, to the right of Paste button
            _resetButton = new Button
            {
                Name = "ResetButton",
                Text = "Reset",
                Width = 50,
                Height = 22,
                Top = headerHeight + rowHeight * 3 + 6,
                Left = labelWidth + 225, // Next to Paste button
                BackColor = UIConfiguration.ResetButtonColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _resetButton.FlatAppearance.BorderColor = UIConfiguration.ResetButtonBorder;
            _resetButton.Click += OnResetClick;

            // Hue row
            var hueLabel = new Label
            {
                Text = "Hue",
                Top = headerHeight + 10,
                Left = 0,
                Width = labelWidth
            };
            _hueSlider = new NoScrollTrackBar
            {
                Name = "HueSlider",
                Minimum = 0,
                Maximum = 360,
                Top = headerHeight,
                Left = labelWidth,
                TickStyle = TickStyle.None,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _hueSlider.ValueChanged += OnSliderValueChanged;

            // Saturation row
            var saturationLabel = new Label
            {
                Text = "Saturation",
                Top = headerHeight + rowHeight + 10,
                Left = 0,
                Width = labelWidth
            };
            _saturationSlider = new NoScrollTrackBar
            {
                Name = "SaturationSlider",
                Minimum = 0,
                Maximum = 100,
                Top = headerHeight + rowHeight,
                Left = labelWidth,
                TickStyle = TickStyle.None,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _saturationSlider.ValueChanged += OnSliderValueChanged;

            // Lightness row
            var lightnessLabel = new Label
            {
                Text = "Lightness",
                Top = headerHeight + rowHeight * 2 + 10,
                Left = 0,
                Width = labelWidth
            };
            _lightnessSlider = new NoScrollTrackBar
            {
                Name = "LightnessSlider",
                Minimum = 0,
                Maximum = 100,
                Top = headerHeight + rowHeight * 2,
                Left = labelWidth,
                TickStyle = TickStyle.None,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _lightnessSlider.ValueChanged += OnSliderValueChanged;

            // Color preview swatch - positioned below sliders, aligned with slider start
            _colorPreviewSwatch = new Panel
            {
                Name = "ColorPreviewSwatch",
                Top = headerHeight + rowHeight * 3 + 5,
                Left = labelWidth,
                Width = 30,
                Height = 30,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Hex color input - positioned next to the swatch
            _hexInput = new TextBox
            {
                Name = "HexInput",
                Top = headerHeight + rowHeight * 3 + 8,
                Left = labelWidth + 40,
                Width = 70
            };
            _hexInput.TextChanged += OnHexInputTextChanged;

            // Copy button - positioned next to hex input
            _copyButton = new Button
            {
                Name = "CopyButton",
                Text = "Copy",
                Top = headerHeight + rowHeight * 3 + 6,
                Left = labelWidth + 115,
                Width = 50,
                Height = 22
            };
            _copyButton.Click += OnCopyClick;

            // Paste button - positioned next to copy button
            _pasteButton = new Button
            {
                Name = "PasteButton",
                Text = "Paste",
                Top = headerHeight + rowHeight * 3 + 6,
                Left = labelWidth + 170,
                Width = 50,
                Height = 22
            };
            _pasteButton.Click += OnPasteClick;

            Controls.Add(_sectionHeaderLabel);
            Controls.Add(_lockCheckbox);
            Controls.Add(_resetButton);
            Controls.Add(hueLabel);
            Controls.Add(_hueSlider);
            Controls.Add(saturationLabel);
            Controls.Add(_saturationSlider);
            Controls.Add(lightnessLabel);
            Controls.Add(_lightnessSlider);
            Controls.Add(_colorPreviewSwatch);
            Controls.Add(_hexInput);
            Controls.Add(_copyButton);
            Controls.Add(_pasteButton);

            // Set picker panel size to fit all controls (including swatch) plus bottom padding
            const int swatchHeight = 30;
            const int swatchTopOffset = 5;
            const int bottomPadding = 10;
            Height = headerHeight + rowHeight * 3 + swatchTopOffset + swatchHeight + bottomPadding;

            // Update slider widths based on current width
            UpdateSliderWidths();
            Resize += (s, e) => UpdateSliderWidths();
        }

        private void UpdateSliderWidths()
        {
            const int labelWidth = 70;
            const int padding = 10;
            var sliderWidth = Width - labelWidth - padding;
            if (sliderWidth > 0)
            {
                _hueSlider.Width = sliderWidth;
                _saturationSlider.Width = sliderWidth;
                _lightnessSlider.Width = sliderWidth;
            }
        }

        private void OnSliderValueChanged(object? sender, EventArgs e)
        {
            UpdateSwatchColor();
            if (!_suppressEvents)
                ColorChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateSwatchColor()
        {
            var color = CurrentColor;
            _colorPreviewSwatch.BackColor = color;

            // Only update hex input if we're not already suppressing events
            // This prevents recursive updates and event firing issues
            var wasSuppressed = _suppressEvents;
            _suppressEvents = true;
            try
            {
                _hexInput.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
            finally
            {
                _suppressEvents = wasSuppressed;
            }
        }

        private void OnHexInputTextChanged(object? sender, EventArgs e)
        {
            if (_suppressEvents) return;

            var text = _hexInput.Text.Trim();
            if (TryParseHexColor(text, out var color))
            {
                _suppressEvents = true;
                try
                {
                    var hsl = HslColor.FromRgb(color);
                    _hueSlider.Value = (int)hsl.H;
                    _saturationSlider.Value = (int)(hsl.S * 100);
                    _lightnessSlider.Value = (int)(hsl.L * 100);
                    _colorPreviewSwatch.BackColor = color;
                }
                finally
                {
                    _suppressEvents = false;
                }
                ColorChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private bool TryParseHexColor(string hex, out Color color)
        {
            color = Color.Empty;
            if (string.IsNullOrEmpty(hex)) return false;

            if (hex.StartsWith("#"))
                hex = hex.Substring(1);

            if (hex.Length != 6) return false;

            try
            {
                var r = Convert.ToInt32(hex.Substring(0, 2), 16);
                var g = Convert.ToInt32(hex.Substring(2, 2), 16);
                var b = Convert.ToInt32(hex.Substring(4, 2), 16);
                color = Color.FromArgb(r, g, b);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void OnResetClick(object? sender, EventArgs e)
        {
            // Check if values actually need to change
            var hueChanged = _hueSlider.Value != _originalHue;
            var satChanged = _saturationSlider.Value != _originalSaturation;
            var lightChanged = _lightnessSlider.Value != _originalLightness;

            if (!hueChanged && !satChanged && !lightChanged)
                return; // Nothing to reset

            _suppressEvents = true;
            try
            {
                _hueSlider.Value = _originalHue;
                _saturationSlider.Value = _originalSaturation;
                _lightnessSlider.Value = _originalLightness;
            }
            finally
            {
                _suppressEvents = false;
            }

            // Fire ResetRequested instead of ColorChanged so the panel can restore
            // the original palette colors (shadow, highlight, accent) from the sprite file
            // rather than regenerating them from the base color
            ResetRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Stores the current slider values as the original values for reset functionality.
        /// Call this after setting the initial color from the palette.
        /// </summary>
        public void StoreOriginalColor()
        {
            _originalHue = _hueSlider.Value;
            _originalSaturation = _saturationSlider.Value;
            _originalLightness = _lightnessSlider.Value;
        }

        /// <summary>
        /// Resets the color picker to its original values.
        /// </summary>
        public void ResetToOriginal()
        {
            OnResetClick(this, EventArgs.Empty);
        }

        public int Hue
        {
            get => _hueSlider.Value;
            set
            {
                _hueSlider.Value = value;
                ColorChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public int Saturation
        {
            get => _saturationSlider.Value;
            set
            {
                _saturationSlider.Value = value;
                ColorChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public int Lightness
        {
            get => _lightnessSlider.Value;
            set
            {
                _lightnessSlider.Value = value;
                ColorChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public Color CurrentColor
        {
            get
            {
                var hsl = new HslColor(Hue, Saturation / 100.0, Lightness / 100.0);
                return hsl.ToRgb();
            }
        }

        public void SetColor(Color color)
        {
            var hsl = HslColor.FromRgb(color);
            Hue = (int)hsl.H;
            Saturation = (int)(hsl.S * 100);
            Lightness = (int)(hsl.L * 100);
        }

        /// <summary>
        /// Sets the color without raising the ColorChanged event.
        /// Use this for initialization to avoid triggering updates.
        /// Also stores as the original color for reset functionality.
        /// </summary>
        public void SetColorSilent(Color color)
        {
            _suppressEvents = true;
            try
            {
                var hsl = HslColor.FromRgb(color);
                _hueSlider.Value = (int)hsl.H;
                _saturationSlider.Value = (int)(hsl.S * 100);
                _lightnessSlider.Value = (int)(hsl.L * 100);

                // Update visual elements
                _colorPreviewSwatch.BackColor = color;
                _hexInput.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

                // Store as original values for reset
                _originalHue = _hueSlider.Value;
                _originalSaturation = _saturationSlider.Value;
                _originalLightness = _lightnessSlider.Value;
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        /// <summary>
        /// Returns the current color as a hex string (e.g., "#FF0000").
        /// </summary>
        public string GetHexColor()
        {
            var color = CurrentColor;
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        /// <summary>
        /// Copies the current color hex value to the clipboard.
        /// </summary>
        public void CopyToClipboard()
        {
            var hexColor = GetHexColor();
            RunOnStaThread(() => Clipboard.SetText(hexColor));
        }

        /// <summary>
        /// Pastes a hex color value from the clipboard and updates the picker.
        /// </summary>
        public void PasteFromClipboard()
        {
            string? text = null;
            RunOnStaThread(() =>
            {
                if (Clipboard.ContainsText())
                {
                    text = Clipboard.GetText();
                }
            });

            if (text != null && TryParseHexColor(text, out var color))
            {
                SetColor(color);
            }
        }

        /// <summary>
        /// Runs an action on a new STA thread. Required for clipboard operations.
        /// </summary>
        private static void RunOnStaThread(Action action)
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                action();
                return;
            }

            Exception? exception = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (exception != null)
            {
                throw exception;
            }
        }

        private void OnCopyClick(object? sender, EventArgs e)
        {
            CopyToClipboard();
        }

        private void OnPasteClick(object? sender, EventArgs e)
        {
            PasteFromClipboard();
        }
    }
}
