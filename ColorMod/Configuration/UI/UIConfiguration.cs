using System.Drawing;

namespace FFTColorCustomizer.Configuration.UI
{
    /// <summary>
    /// Centralized UI configuration constants and colors
    /// </summary>
    public static class UIConfiguration
    {
        // Form dimensions
        public const int FormWidth = 700;
        public const int FormHeight = 730;
        public const int ButtonPanelHeight = 40;
        public const int ButtonWidth = 80;
        public const int ButtonHeight = 30;
        public const int ButtonPadding = 10;

        // Preview dimensions
        public const int PreviewSize = 72;  // Medium preview size - good balance of visibility and space

        // Layout settings
        public const int MainPanelPadding = 10;
        public const int MaxDropDownItems = 30;

        // Dark theme colors
        public static readonly Color DarkBackground = Color.FromArgb(30, 30, 30);
        public static readonly Color DarkerBackground = Color.FromArgb(25, 25, 25);
        public static readonly Color HeaderBackground = Color.FromArgb(40, 40, 40);
        public static readonly Color ButtonBackground = Color.FromArgb(50, 50, 50);
        public static readonly Color ButtonBorder = Color.FromArgb(100, 100, 100);
        public static readonly Color ComboBoxBackground = Color.FromArgb(45, 45, 45);
        public static readonly Color PreviewBackground = Color.FromArgb(50, 50, 50);

        // Button colors
        public static readonly Color SaveButtonColor = Color.FromArgb(150, 30, 30);
        public static readonly Color SaveButtonBorder = Color.FromArgb(220, 50, 50);
        public static readonly Color SaveButtonHover = Color.FromArgb(180, 40, 40);
        public static readonly Color SaveButtonHoverBorder = Color.FromArgb(255, 60, 60);

        public static readonly Color ResetButtonColor = Color.FromArgb(80, 80, 30);
        public static readonly Color ResetButtonBorder = Color.FromArgb(150, 150, 50);
        public static readonly Color ResetButtonHover = Color.FromArgb(100, 100, 40);
        public static readonly Color ResetButtonHoverBorder = Color.FromArgb(200, 200, 60);

        public static readonly Color CancelButtonHover = Color.FromArgb(70, 70, 70);
        public static readonly Color CancelButtonHoverBorder = Color.FromArgb(150, 150, 150);

        public static readonly Color RandomizeButtonColor = Color.FromArgb(60, 40, 100);
        public static readonly Color RandomizeButtonBorder = Color.FromArgb(120, 80, 180);
        public static readonly Color RandomizeButtonHover = Color.FromArgb(80, 55, 130);
        public static readonly Color RandomizeButtonHoverBorder = Color.FromArgb(150, 100, 220);

        // Text colors
        public static readonly Color TextColor = Color.White;

        // Fonts
        public static readonly Font HeaderFont = new Font("Arial", 12, FontStyle.Bold);
        public static readonly Font SectionHeaderFont = new Font("Arial", 10, FontStyle.Bold);
    }
}
