using System;
using System.Drawing;
using System.Windows.Forms;

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
        private bool _suppressEvents;
        private int _originalHue;
        private int _originalSaturation;
        private int _originalLightness;

        public event EventHandler ColorChanged;

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
            Margin = new Padding(0, 0, 0, 15);
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            const int headerHeight = 25;
            const int rowHeight = 45;
            const int labelWidth = 70;
            const int padding = 10;

            // Section header label - larger, bold, dark red for visibility
            _sectionHeaderLabel = new Label
            {
                Name = "SectionHeaderLabel",
                Text = _sectionName ?? "",
                Top = 5,
                Left = 0,
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 11f, System.Drawing.FontStyle.Bold),
                ForeColor = Color.DarkRed
            };

            // Reset button for this section
            _resetButton = new Button
            {
                Name = "ResetButton",
                Text = "Reset",
                Width = 50,
                Height = 22,
                Top = 0,
                Left = 200,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
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

            Controls.Add(_sectionHeaderLabel);
            Controls.Add(_resetButton);
            Controls.Add(hueLabel);
            Controls.Add(_hueSlider);
            Controls.Add(saturationLabel);
            Controls.Add(_saturationSlider);
            Controls.Add(lightnessLabel);
            Controls.Add(_lightnessSlider);

            // Set picker panel size to fit all controls plus bottom padding for separation
            const int bottomPadding = 20;
            Height = headerHeight + rowHeight * 3 + bottomPadding;

            // Update slider widths based on current width
            UpdateSliderWidths();
            Resize += (s, e) => UpdateSliderWidths();
        }

        private void UpdateSliderWidths()
        {
            const int labelWidth = 70;
            const int padding = 10;
            const int resetButtonWidth = 50;
            var sliderWidth = Width - labelWidth - padding;
            if (sliderWidth > 0)
            {
                _hueSlider.Width = sliderWidth;
                _saturationSlider.Width = sliderWidth;
                _lightnessSlider.Width = sliderWidth;
            }
            // Position reset button at right edge of panel
            if (Width > resetButtonWidth + padding)
            {
                _resetButton.Left = Width - resetButtonWidth - padding;
            }
        }

        private void OnSliderValueChanged(object? sender, EventArgs e)
        {
            if (!_suppressEvents)
                ColorChanged?.Invoke(this, EventArgs.Empty);
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
            ColorChanged?.Invoke(this, EventArgs.Empty);
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
    }
}
