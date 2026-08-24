using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using FFTColorCustomizer.Configuration.UI;

namespace FFTColorCustomizer.Configuration
{
    public partial class ConfigurationForm
    {
        private void InitializeFormProperties()
        {
            Text = "FFT Color Customizer - Configuration";

            var saved = WindowStateService.Load(_configPath);
            var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            var (w, h) = WindowStateService.ResolveSize(
                saved,
                defaultWidth: UIConfiguration.FormWidth,
                defaultHeight: UIConfiguration.FormHeight,
                minWidth: UIConfiguration.FormMinWidth,
                minHeight: UIConfiguration.FormMinHeight,
                maxWidth: workingArea.Width,
                maxHeight: workingArea.Height);

            Size = new Size(w, h);
            MinimumSize = new Size(UIConfiguration.FormMinWidth, UIConfiguration.FormMinHeight);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScroll = false;
            TopMost = true;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = UIConfiguration.DarkBackground;
            ForeColor = UIConfiguration.TextColor;
        }

        private void CreateTitleBar()
        {
            _titleBar = new CustomTitleBar(this, "FFT Color Customizer - Configuration");
            Controls.Add(_titleBar);
        }

        private void CreateMainContentPanel()
        {
            // Create main content panel that will be below the title bar
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UIConfiguration.DarkBackground,
                AutoScroll = true
            };

            _mainPanel = ConfigUIComponentFactory.CreateMainPanel();

            // Owner cosmetic pass: the title bar left ~15px of dead space above the "Themes"
            // heading (this panel's own 10px top padding plus the header label's own 5px).
            // Shrink just the top inset -- left/right/bottom stay at the factory default
            // (UIConfiguration.MainPanelPadding) since every other row still wants that room.
            _mainPanel.Padding = new Padding(
                UIConfiguration.MainPanelPadding, 2, UIConfiguration.MainPanelPadding, UIConfiguration.MainPanelPadding);

            // Add header
            var headerLabel = ConfigUIComponentFactory.CreateCharacterLabel("Themes", true);
            headerLabel.Font = UIConfiguration.HeaderFont;
            headerLabel.TextAlign = ContentAlignment.MiddleCenter;
            _mainPanel.SetColumnSpan(headerLabel, 3);
            _mainPanel.Controls.Add(headerLabel, 0, 0);

            _contentPanel.Controls.Add(_mainPanel);
            Controls.Add(_contentPanel);
            // Dock layout processes back-to-front; the Fill panel must sit in front
            // of the Top-docked title bar or it extends underneath it.
            _contentPanel.BringToFront();
        }

        private void CreateButtonPanel()
        {
            var buttonPanel = ConfigUIComponentFactory.CreateButtonPanel();

            // Create Cancel button
            _cancelButton = ConfigUIComponentFactory.CreateCancelButton();
            _cancelButton.Location = new Point(buttonPanel.Width - 95, 5);
            _cancelButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _cancelButton.Click += (s, e) => Close();

            // Create Save button
            _saveButton = ConfigUIComponentFactory.CreateSaveButton();
            _saveButton.Location = new Point(buttonPanel.Width - 180, 5);
            _saveButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _saveButton.Click += SaveButton_Click;

            // Create Reset All button
            _resetAllButton = ConfigUIComponentFactory.CreateResetAllButton();
            _resetAllButton.Location = new Point(UIConfiguration.ButtonPadding, 5);
            _resetAllButton.Click += ResetAllButton_Click;

            // Create tip label
            var tipLabel = new Label
            {
                Text = "Tip: Scroll your mouse wheel while hovering over a drop down list",
                AutoSize = true,
                ForeColor = Color.FromArgb(180, 180, 180), // Light gray for subtle text
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                Location = new Point(-100, 10), // Centered position
                Anchor = AnchorStyles.Top
            };

            buttonPanel.Controls.Add(_cancelButton);
            buttonPanel.Controls.Add(_saveButton);
            buttonPanel.Controls.Add(_resetAllButton);
            buttonPanel.Controls.Add(tipLabel);

            Controls.Add(buttonPanel);
        }

        private readonly List<Label> _collapsibleHeaders = new();

        private Label CreateCollapsibleHeader(string text, bool collapsed, int row)
        {
            var header = ConfigUIComponentFactory.CreateCharacterLabel(
                collapsed ? $"▶ {text}" : $"▼ {text}",
                true
            );

            _mainPanel.SetColumnSpan(header, 3);
            _mainPanel.Controls.Add(header, 0, row);

            // Count==0 guards the one-time Resize subscription. If ReloadForm ever starts
            // clearing _collapsibleHeaders, move this subscription to the constructor or
            // it will re-subscribe on every rebuild.
            if (_collapsibleHeaders.Count == 0)
                Resize += (s, e) => AlignCollapsibleHeaders();
            _collapsibleHeaders.Add(header);
            header.TextChanged += (s, e) => AlignCollapsibleHeaders();
            AlignCollapsibleHeaders();

            return header;
        }

        /// <summary>
        /// Headers read as a centered block with a shared left edge: every title starts at
        /// the same x (so the collapse arrows form one column), and that edge sits where the
        /// WIDEST title would be centered in the window. The pad derives from the FORM's
        /// width, never the label's own size: padding feeds the label's preferred size,
        /// which feeds this auto-sizing table's column widths, so a label-size-driven pad
        /// is a feedback loop (it collapsed the headers to text width when tried).
        /// </summary>
        private void AlignCollapsibleHeaders()
        {
            int maxTextWidth = 0;
            foreach (var h in _collapsibleHeaders)
                maxTextWidth = Math.Max(maxTextWidth,
                    TextRenderer.MeasureText(h.Text, h.Font).Width);

            int pad = Math.Max(0, (ClientSize.Width - maxTextWidth) / 2);
            foreach (var h in _collapsibleHeaders)
            {
                if (h.Padding.Left != pad)
                    h.Padding = new Padding(pad, 5, 0, 5);
            }
        }
    }
}
