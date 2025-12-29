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
        private TrackBar _hueSlider;
        private TrackBar _saturationSlider;
        private TrackBar _lightnessSlider;
        private Label _sectionHeaderLabel;
        private bool _suppressEvents;

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
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            const int headerHeight = 25;
            const int rowHeight = 45;
            const int labelWidth = 70;
            const int padding = 10;

            // Section header label
            _sectionHeaderLabel = new Label
            {
                Name = "SectionHeaderLabel",
                Text = _sectionName ?? "",
                Top = 0,
                Left = 0,
                AutoSize = true,
                Font = new System.Drawing.Font(System.Drawing.SystemFonts.DefaultFont, System.Drawing.FontStyle.Bold)
            };

            // Hue row
            var hueLabel = new Label
            {
                Text = "Hue",
                Top = headerHeight + 10,
                Left = 0,
                Width = labelWidth
            };
            _hueSlider = new TrackBar
            {
                Name = "HueSlider",
                Minimum = 0,
                Maximum = 360,
                Top = headerHeight,
                Left = labelWidth,
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
            _saturationSlider = new TrackBar
            {
                Name = "SaturationSlider",
                Minimum = 0,
                Maximum = 100,
                Top = headerHeight + rowHeight,
                Left = labelWidth,
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
            _lightnessSlider = new TrackBar
            {
                Name = "LightnessSlider",
                Minimum = 0,
                Maximum = 100,
                Top = headerHeight + rowHeight * 2,
                Left = labelWidth,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _lightnessSlider.ValueChanged += OnSliderValueChanged;

            Controls.Add(_sectionHeaderLabel);
            Controls.Add(hueLabel);
            Controls.Add(_hueSlider);
            Controls.Add(saturationLabel);
            Controls.Add(_saturationSlider);
            Controls.Add(lightnessLabel);
            Controls.Add(_lightnessSlider);

            // Set picker panel size to fit all controls
            Height = headerHeight + rowHeight * 3;

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
            if (!_suppressEvents)
                ColorChanged?.Invoke(this, EventArgs.Empty);
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
            }
            finally
            {
                _suppressEvents = false;
            }
        }
    }
}
