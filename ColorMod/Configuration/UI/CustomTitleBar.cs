using System;
using System.Drawing;
using System.Windows.Forms;

namespace FFTColorMod.Configuration.UI
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
            BackColor = Color.FromArgb(20, 20, 20);  // Darker than main background

            // Title label
            _titleLabel = new Label
            {
                Text = title,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = new Point(10, 5),
                AutoSize = true
            };

            // Close button
            _closeButton = new Button
            {
                Text = "✕",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(20, 20, 20),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(30, 25),
                Location = new Point(parentForm.Width - 35, 2),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _closeButton.FlatAppearance.BorderSize = 0;
            _closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);  // Red on hover
            _closeButton.Click += (s, e) => parentForm.Close();

            // Minimize button
            _minimizeButton = new Button
            {
                Text = "─",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(20, 20, 20),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(30, 25),
                Location = new Point(parentForm.Width - 70, 2),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _minimizeButton.FlatAppearance.BorderSize = 0;
            _minimizeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 50);
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