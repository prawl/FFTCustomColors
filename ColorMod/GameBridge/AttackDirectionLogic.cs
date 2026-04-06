namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure logic for computing which arrow key reaches a target tile,
    /// given the empirically-detected "Right" direction delta.
    ///
    /// The rotation table forms a cycle: Right → Down → Left → Up
    /// Each is a 90° clockwise rotation of the previous:
    ///   Down = rotate Right 90° CW = (Right.dy, -Right.dx)
    ///   Left = opposite of Right = (-Right.dx, -Right.dy)
    ///   Up = rotate Right 90° CCW = (-Right.dy, Right.dx)
    /// </summary>
    public static class AttackDirectionLogic
    {
        /// <summary>
        /// Given what the Right arrow key does (rightDx, rightDy) from empirical detection,
        /// compute which arrow key produces the desired (targetDx, targetDy).
        /// Returns "Right", "Left", "Up", "Down", or null if the target delta isn't cardinal.
        /// </summary>
        public static string? ComputeArrowForDelta(int rightDx, int rightDy, int targetDx, int targetDy)
        {
            if (targetDx == 0 && targetDy == 0) return null;

            // Right = (rdx, rdy)
            if (targetDx == rightDx && targetDy == rightDy) return "Right";

            // Left = opposite of Right
            if (targetDx == -rightDx && targetDy == -rightDy) return "Left";

            // Down = 90° CW from Right = (rdy, -rdx)
            int downDx = rightDy, downDy = -rightDx;
            if (targetDx == downDx && targetDy == downDy) return "Down";

            // Up = 90° CCW from Right = (-rdy, rdx)
            int upDx = -rightDy, upDy = rightDx;
            if (targetDx == upDx && targetDy == upDy) return "Up";

            return null; // Not a cardinal direction match
        }
    }
}
