using System;
using System.Drawing;
using System.Windows.Forms;

namespace FFTColorCustomizer.Configuration.UI
{
    /// <summary>
    /// Factory for creating consistent UI components
    /// </summary>
    public static class ConfigUIComponentFactory
    {
        public static ComboBox CreateThemeComboBox()
        {
            return new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                MaxDropDownItems = UIConfiguration.MaxDropDownItems,
                BackColor = UIConfiguration.ComboBoxBackground,
                ForeColor = UIConfiguration.TextColor,
                FlatStyle = FlatStyle.Flat
            };
        }

        public static void ApplyThemeComboBoxStyling(ComboBox comboBox)
        {
            comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox.Dock = DockStyle.Fill;
            comboBox.MaxDropDownItems = UIConfiguration.MaxDropDownItems;
            comboBox.BackColor = UIConfiguration.ComboBoxBackground;
            comboBox.ForeColor = UIConfiguration.TextColor;
            comboBox.FlatStyle = FlatStyle.Flat;
        }

        public static PictureBox CreatePreviewPictureBox()
        {
            return new PictureBox
            {
                Size = new Size(UIConfiguration.PreviewSize, UIConfiguration.PreviewSize),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill,
                BackColor = UIConfiguration.PreviewBackground
            };
        }

        public static Label CreateCharacterLabel(string text, bool isHeader = false)
        {
            var label = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = isHeader ? ContentAlignment.MiddleCenter : ContentAlignment.TopLeft,
                ForeColor = UIConfiguration.TextColor
            };

            if (isHeader)
            {
                label.Font = UIConfiguration.SectionHeaderFont;
                label.BackColor = UIConfiguration.HeaderBackground;
                label.Padding = new Padding(0, 5, 0, 5);
                label.Cursor = Cursors.Hand;
            }
            else
            {
                label.Padding = new Padding(0, 3, 0, 0);
            }

            return label;
        }

        public static Button CreateStyledButton(string text, Color baseColor, Color borderColor,
            Color hoverColor, Color hoverBorderColor)
        {
            var button = new Button
            {
                Text = text,
                Width = UIConfiguration.ButtonWidth,
                Height = UIConfiguration.ButtonHeight,
                BackColor = baseColor,
                ForeColor = UIConfiguration.TextColor,
                FlatStyle = FlatStyle.Flat
            };

            button.FlatAppearance.BorderColor = borderColor;
            button.FlatAppearance.BorderSize = 1;

            // Add hover effects
            button.MouseEnter += (s, e) =>
            {
                button.BackColor = hoverColor;
                button.FlatAppearance.BorderColor = hoverBorderColor;
            };

            button.MouseLeave += (s, e) =>
            {
                button.BackColor = baseColor;
                button.FlatAppearance.BorderColor = borderColor;
            };

            return button;
        }

        public static Button CreateSaveButton()
        {
            return CreateStyledButton("Save",
                UIConfiguration.SaveButtonColor,
                UIConfiguration.SaveButtonBorder,
                UIConfiguration.SaveButtonHover,
                UIConfiguration.SaveButtonHoverBorder);
        }

        public static Button CreateCancelButton()
        {
            return CreateStyledButton("Cancel",
                UIConfiguration.ButtonBackground,
                UIConfiguration.ButtonBorder,
                UIConfiguration.CancelButtonHover,
                UIConfiguration.CancelButtonHoverBorder);
        }

        public static Button CreateResetAllButton()
        {
            return CreateStyledButton("Reset All",
                UIConfiguration.ResetButtonColor,
                UIConfiguration.ResetButtonBorder,
                UIConfiguration.ResetButtonHover,
                UIConfiguration.ResetButtonHoverBorder);
        }

        public static Panel CreateButtonPanel()
        {
            return new Panel
            {
                Dock = DockStyle.Bottom,
                Height = UIConfiguration.ButtonPanelHeight,
                BackColor = UIConfiguration.DarkerBackground
            };
        }

        public static TableLayoutPanel CreateMainPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                RowCount = 50,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(UIConfiguration.MainPanelPadding),
                BackColor = UIConfiguration.DarkBackground,
                ForeColor = UIConfiguration.TextColor
            };

            // Add column styles
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

            return panel;
        }
    }
}
