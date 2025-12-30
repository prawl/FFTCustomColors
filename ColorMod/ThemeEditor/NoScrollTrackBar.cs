using System.Windows.Forms;

namespace FFTColorCustomizer.ThemeEditor
{
    /// <summary>
    /// A TrackBar that ignores mouse wheel events to prevent accidental value changes
    /// when scrolling the parent container.
    /// </summary>
    public class NoScrollTrackBar : TrackBar
    {
        private const int WM_MOUSEWHEEL = 0x020A;

        protected override void WndProc(ref Message m)
        {
            // Intercept mouse wheel messages at the Windows message level
            if (m.Msg == WM_MOUSEWHEEL)
            {
                // Ignore mouse wheel - don't pass to base
                return;
            }

            base.WndProc(ref m);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // Intentionally do nothing - ignore mouse wheel input
            // This prevents accidental slider changes when scrolling the color pickers panel
        }
    }
}
