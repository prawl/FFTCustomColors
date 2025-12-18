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
            Text = "FFT Color Mod - Configuration";
            Size = new Size(UIConfiguration.FormWidth, UIConfiguration.FormHeight);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScroll = false;
            TopMost = true;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = UIConfiguration.DarkBackground;
            ForeColor = UIConfiguration.TextColor;
        }

        private void CreateTitleBar()
        {
            _titleBar = new CustomTitleBar(this, "FFT Color Mod - Configuration");
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

            // Create Debug button for testing carousel
            var debugButton = new Button
            {
                Text = "Debug Carousel",
                Size = new Size(100, 30),
                Location = new Point(200, 5),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.Yellow,
                Font = new Font("Segoe UI", 9F)
            };
            debugButton.FlatAppearance.BorderColor = Color.Yellow;
            debugButton.Click += (s, e) => DebugCarousel();

            buttonPanel.Controls.Add(_cancelButton);
            buttonPanel.Controls.Add(_saveButton);
            buttonPanel.Controls.Add(_resetAllButton);
            buttonPanel.Controls.Add(debugButton);

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
