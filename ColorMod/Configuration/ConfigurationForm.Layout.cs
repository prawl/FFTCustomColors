using System;
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
            Size = new Size(UIConfiguration.FormWidth, UIConfiguration.FormHeight);
            MinimumSize = new Size(400, 300);
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

            // Add header
            var headerLabel = ConfigUIComponentFactory.CreateCharacterLabel("Themes", true);
            headerLabel.Font = UIConfiguration.HeaderFont;
            headerLabel.TextAlign = ContentAlignment.MiddleCenter;
            _mainPanel.SetColumnSpan(headerLabel, 3);
            _mainPanel.Controls.Add(headerLabel, 0, 0);

            _contentPanel.Controls.Add(_mainPanel);
            Controls.Add(_contentPanel);
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

        private Label CreateCollapsibleHeader(string text, bool collapsed, int row)
        {
            var header = ConfigUIComponentFactory.CreateCharacterLabel(
                collapsed ? $"▶ {text}" : $"▼ {text}",
                true
            );

            _mainPanel.SetColumnSpan(header, 3);
            _mainPanel.Controls.Add(header, 0, row);

            return header;
        }
    }
}
