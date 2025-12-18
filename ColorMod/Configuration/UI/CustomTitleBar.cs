using System.Drawing;
using System.Windows.Forms;

namespace FFTColorCustomizer.Configuration.UI
{
    public class CustomTitleBar : Panel
    {
        private readonly Form _parentForm;
        private readonly Label _titleLabel;
        private readonly Button _closeButton;
        private readonly Button _minimizeButton;

        private bool _isDragging = false;
        private Point _dragCursorPoint;
        private Point _dragFormPoint;

        public CustomTitleBar(Form parentForm, string title)
        {
            _parentForm = parentForm;

            Height = 30;
            Dock = DockStyle.Top;
            BackColor = Color.FromArgb(121, 57, 57);  // Reloaded-II red (#793939)

            // Title label
            _titleLabel = new Label
            {
                Text = title,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = new Point(10, 5),
                AutoSize = true
            };

            // Close button - Make it more visible with white background initially
            _closeButton = new Button
            {
                Text = "✕",
                ForeColor = Color.White,
                BackColor = Color.Transparent,  // Transparent background
                FlatStyle = FlatStyle.Flat,
                Size = new Size(35, 26),
                Location = new Point(this.Width - 40, 2),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Segoe UI", 12, FontStyle.Regular),  // Clean, regular font
                Cursor = Cursors.Hand
            };
            _closeButton.FlatAppearance.BorderSize = 0;  // No border
            _closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);  // Bright red on hover
            _closeButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(180, 17, 35);  // Darker red on click
            _closeButton.Click += (s, e) =>
            {
                parentForm.DialogResult = DialogResult.Cancel;
                parentForm.Close();
            };

            // Minimize button
            _minimizeButton = new Button
            {
                Text = "─",
                ForeColor = Color.White,
                BackColor = Color.Transparent,  // Transparent background
                FlatStyle = FlatStyle.Flat,
                Size = new Size(35, 26),
                Location = new Point(this.Width - 80, 2),  // Position to the left of close button
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            _minimizeButton.FlatAppearance.BorderSize = 0;  // No border
            _minimizeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(100, 50, 50);  // Darker red on hover
            _minimizeButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(80, 40, 40);  // Even darker on click
            _minimizeButton.Click += (s, e) => parentForm.WindowState = FormWindowState.Minimized;

            Controls.Add(_titleLabel);
            Controls.Add(_closeButton);
            Controls.Add(_minimizeButton);

            // Enable dragging
            SetupDragging();
        }

        private void SetupDragging()
        {
            MouseDown += TitleBar_MouseDown;
            MouseMove += TitleBar_MouseMove;
            MouseUp += TitleBar_MouseUp;
            _titleLabel.MouseDown += TitleBar_MouseDown;
            _titleLabel.MouseMove += TitleBar_MouseMove;
            _titleLabel.MouseUp += TitleBar_MouseUp;
        }

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            _isDragging = true;
            _dragCursorPoint = Cursor.Position;
            _dragFormPoint = _parentForm.Location;
        }

        private void TitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                Point diff = Point.Subtract(Cursor.Position, new Size(_dragCursorPoint));
                _parentForm.Location = Point.Add(_dragFormPoint, new Size(diff));
            }
        }

        private void TitleBar_MouseUp(object sender, MouseEventArgs e)
        {
            _isDragging = false;
        }
    }
}
