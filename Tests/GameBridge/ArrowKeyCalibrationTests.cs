using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class ArrowKeyCalibrationTests
    {
        [Fact]
        public void BuildPath_SingleStepEast_ReturnsRightKey()
        {
            // Calibration: Right key gives dx=+1, dy=0.
            var cal = new ArrowKeyCalibration(
                right: (1, 0), left: (-1, 0), up: (0, -1), down: (0, 1));

            var path = cal.BuildPath(fromX: 5, fromY: 5, toX: 6, toY: 5);

            Assert.Equal(new[] { ArrowKeyCalibration.Key.Right }, path);
        }

        [Fact]
        public void BuildPath_TwoStepsSouth_ReturnsTwoDowns()
        {
            var cal = new ArrowKeyCalibration(
                right: (1, 0), left: (-1, 0), up: (0, -1), down: (0, 1));

            var path = cal.BuildPath(5, 5, 5, 7);

            Assert.Equal(
                new[] { ArrowKeyCalibration.Key.Down, ArrowKeyCalibration.Key.Down },
                path);
        }

        [Fact]
        public void BuildPath_DiagonalNE_ReturnsUpAndRight()
        {
            var cal = new ArrowKeyCalibration(
                right: (1, 0), left: (-1, 0), up: (0, -1), down: (0, 1));

            var path = cal.BuildPath(5, 5, 6, 4);

            Assert.Equal(2, path.Length);
            Assert.Contains(ArrowKeyCalibration.Key.Right, path);
            Assert.Contains(ArrowKeyCalibration.Key.Up, path);
        }

        [Fact]
        public void BuildPath_RotatedMapping_LeftKeyGoesSouth()
        {
            // Camera rotated 90 CW: Left key in screen-space moves SOUTH in map-space (dy=+1).
            var cal = new ArrowKeyCalibration(
                right: (0, -1), left: (0, 1), up: (1, 0), down: (-1, 0));

            var path = cal.BuildPath(5, 5, 5, 7);

            Assert.Equal(
                new[] { ArrowKeyCalibration.Key.Left, ArrowKeyCalibration.Key.Left },
                path);
        }

        [Fact]
        public void BuildPath_ZeroDistance_ReturnsEmptyPath()
        {
            var cal = new ArrowKeyCalibration(
                right: (1, 0), left: (-1, 0), up: (0, -1), down: (0, 1));

            var path = cal.BuildPath(5, 5, 5, 5);

            Assert.Empty(path);
        }

        [Fact]
        public void FromObservations_BuildsMappingFromFourKeyPresses()
        {
            // User presses each arrow once starting at (5,5), observes cursor movement.
            var cal = ArrowKeyCalibration.FromObservations(
                rightDelta: (1, 0),
                leftDelta: (-1, 0),
                upDelta: (0, -1),
                downDelta: (0, 1));

            // Navigating east should produce a Right keypress.
            var path = cal.BuildPath(0, 0, 1, 0);
            Assert.Single(path);
            Assert.Equal(ArrowKeyCalibration.Key.Right, path[0]);
        }
    }
}
