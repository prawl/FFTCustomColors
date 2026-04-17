namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Classifies virtual-key codes into "nav" (cursor movement inside a menu)
    /// vs "transition" (screen change, confirm, cancel, tab swap) and returns
    /// the appropriate post-key delay.
    ///
    /// Before 2026-04-17 every key slept the same 350ms regardless of kind,
    /// which was wasteful on nav-heavy flows (e.g. open_eqa navigating the
    /// party grid fires up to 8 cursor keys back-to-back — 8 × 350 = 2.8s
    /// just for positioning). Shorter nav delay trims those flows noticeably
    /// while keeping the safer 350ms on Enter/Escape/Tab where animations
    /// can actually drop a follow-up key.
    ///
    /// Values below are conservative. Tune by watching the `[i=N, +Nms]`
    /// timing log for dropped keys on nav-heavy screens — if we see drops,
    /// raise NAV_DELAY_MS first before touching TRANSITION_DELAY_MS.
    /// </summary>
    public static class KeyDelayClassifier
    {
        /// <summary>Cursor movement inside a stable menu. Highlight bar only.</summary>
        public const int NAV_DELAY_MS = 200;

        /// <summary>Screen change, confirm, cancel, tab swap. Animation budget.</summary>
        public const int TRANSITION_DELAY_MS = 350;

        // VK constants inline here so the classifier stays self-contained and
        // testable without dragging the full NavigationActions VK zoo in.
        private const int VK_UP     = 0x26;
        private const int VK_DOWN   = 0x28;
        private const int VK_LEFT   = 0x25;
        private const int VK_RIGHT  = 0x27;

        public static int DelayMsFor(int vk)
        {
            switch (vk)
            {
                case VK_UP:
                case VK_DOWN:
                case VK_LEFT:
                case VK_RIGHT:
                    return NAV_DELAY_MS;
                default:
                    // Transition, tab-cycle, or unknown — all get the safer
                    // 350ms. See class-level docs for the rationale.
                    return TRANSITION_DELAY_MS;
            }
        }
    }
}
