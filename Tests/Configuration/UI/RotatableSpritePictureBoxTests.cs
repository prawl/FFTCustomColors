using System;
using System.Threading;
using System.Windows.Forms;
using Xunit;
using FFTColorCustomizer.Configuration.UI;

namespace FFTColorCustomizer.Tests.Configuration.UI
{
    /// <summary>
    /// Tests for the rotation-arrow overlay on the theme-editor sprite preview.
    /// Mirrors the PreviewCarousel pattern: arrows are painted on the picture
    /// itself; left-third and right-third clicks raise rotation events.
    /// </summary>
    public class RotatableSpritePictureBoxTests
    {
        [Fact]
        [STAThread]
        public void Click_OnLeftThird_RaisesRotateLeftRequested()
        {
            using var box = new RotatableSpritePictureBox { Width = 300, Height = 200 };
            int leftEvents = 0, rightEvents = 0;
            box.RotateLeftRequested += (_, _) => leftEvents++;
            box.RotateRightRequested += (_, _) => rightEvents++;

            box.SimulateClickAt(50, 100); // x=50 of 300 → left third

            Assert.Equal(1, leftEvents);
            Assert.Equal(0, rightEvents);
        }

        [Fact]
        [STAThread]
        public void Click_OnRightThird_RaisesRotateRightRequested()
        {
            using var box = new RotatableSpritePictureBox { Width = 300, Height = 200 };
            int leftEvents = 0, rightEvents = 0;
            box.RotateLeftRequested += (_, _) => leftEvents++;
            box.RotateRightRequested += (_, _) => rightEvents++;

            box.SimulateClickAt(250, 100); // x=250 of 300 → right third

            Assert.Equal(0, leftEvents);
            Assert.Equal(1, rightEvents);
        }

        [Fact]
        [STAThread]
        public void Click_OnMiddleThird_RaisesNoEvent()
        {
            using var box = new RotatableSpritePictureBox { Width = 300, Height = 200 };
            int events = 0;
            box.RotateLeftRequested += (_, _) => events++;
            box.RotateRightRequested += (_, _) => events++;

            box.SimulateClickAt(150, 100); // x=150 of 300 → middle third

            Assert.Equal(0, events);
        }

        [Fact]
        [STAThread]
        public void Subclass_OfStoneTilePictureBox_KeepsBackgroundTilePainting()
        {
            // The whole point of subclassing StoneTilePictureBox is to inherit the
            // stone-floor background. Make sure that lineage isn't lost.
            using var box = new RotatableSpritePictureBox();
            Assert.IsAssignableFrom<StoneTilePictureBox>(box);
        }
    }
}
