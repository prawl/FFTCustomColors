using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Maps screen-space arrow keys to map-space (dx, dy) for the current
    /// camera rotation. Calibrate once per Move mode entry by pressing each
    /// key and observing the cursor delta. Then BuildPath() turns a target
    /// (x,y) into an arrow-key sequence.
    /// </summary>
    public class ArrowKeyCalibration
    {
        public enum Key { Right, Left, Up, Down }

        public (int dx, int dy) Right { get; }
        public (int dx, int dy) Left { get; }
        public (int dx, int dy) Up { get; }
        public (int dx, int dy) Down { get; }

        public ArrowKeyCalibration(
            (int dx, int dy) right,
            (int dx, int dy) left,
            (int dx, int dy) up,
            (int dx, int dy) down)
        {
            Right = right;
            Left = left;
            Up = up;
            Down = down;
        }

        public static ArrowKeyCalibration FromObservations(
            (int dx, int dy) rightDelta,
            (int dx, int dy) leftDelta,
            (int dx, int dy) upDelta,
            (int dx, int dy) downDelta)
        {
            return new ArrowKeyCalibration(rightDelta, leftDelta, upDelta, downDelta);
        }

        public Key[] BuildPath(int fromX, int fromY, int toX, int toY)
        {
            int dx = toX - fromX;
            int dy = toY - fromY;
            var keys = new List<Key>();

            // Pick the key whose delta best matches dx direction (x axis)
            if (dx != 0)
            {
                var xKey = PickKeyForAxis(dx, 0);
                int xSteps = AbsComponent(dx, 0);
                for (int i = 0; i < xSteps; i++) keys.Add(xKey);
            }
            if (dy != 0)
            {
                var yKey = PickKeyForAxis(0, dy);
                int ySteps = AbsComponent(0, dy);
                for (int i = 0; i < ySteps; i++) keys.Add(yKey);
            }

            return keys.ToArray();
        }

        private Key PickKeyForAxis(int wantDx, int wantDy)
        {
            // Return whichever of the 4 keys has a delta in the desired direction.
            // Match sign of wantDx and wantDy; only one will be non-zero here.
            if (MatchesDirection(Right, wantDx, wantDy)) return Key.Right;
            if (MatchesDirection(Left, wantDx, wantDy)) return Key.Left;
            if (MatchesDirection(Up, wantDx, wantDy)) return Key.Up;
            if (MatchesDirection(Down, wantDx, wantDy)) return Key.Down;
            // Fallback (shouldn't hit under valid calibration)
            return Key.Right;
        }

        private static bool MatchesDirection((int dx, int dy) delta, int wantDx, int wantDy)
        {
            if (wantDx > 0) return delta.dx > 0 && delta.dy == 0;
            if (wantDx < 0) return delta.dx < 0 && delta.dy == 0;
            if (wantDy > 0) return delta.dy > 0 && delta.dx == 0;
            if (wantDy < 0) return delta.dy < 0 && delta.dx == 0;
            return false;
        }

        private int AbsComponent(int wantDx, int wantDy)
        {
            if (wantDx != 0) return wantDx > 0 ? wantDx : -wantDx;
            if (wantDy != 0) return wantDy > 0 ? wantDy : -wantDy;
            return 0;
        }
    }
}
