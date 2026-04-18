using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class KeyDelayClassifierTests
    {
        // Virtual-key codes. Match the constants in NavigationActions.cs.
        private const int VK_UP     = 0x26;
        private const int VK_DOWN   = 0x28;
        private const int VK_LEFT   = 0x25;
        private const int VK_RIGHT  = 0x27;
        private const int VK_RETURN = 0x0D;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_SPACE  = 0x20;
        private const int VK_TAB    = 0x09;
        private const int VK_Q      = 0x51;
        private const int VK_E      = 0x45;
        private const int VK_A      = 0x41;
        private const int VK_D      = 0x44;
        private const int VK_F      = 0x46;

        // Nav keys: cursor movement inside a menu. The animation is cheap
        // (highlight bar moves one cell), so we can use a shorter delay.
        [Theory]
        [InlineData(VK_UP)]
        [InlineData(VK_DOWN)]
        [InlineData(VK_LEFT)]
        [InlineData(VK_RIGHT)]
        public void NavKeys_GetNavDelay(int vk)
        {
            Assert.Equal(
                KeyDelayClassifier.NAV_DELAY_MS,
                KeyDelayClassifier.DelayMsFor(vk));
        }

        // Transition keys: open a new screen, close a dialog, or commit to
        // an action. Each one can trigger 200-500ms of animation/load, so
        // the longer delay protects against dropped follow-up keys.
        [Theory]
        [InlineData(VK_RETURN)]
        [InlineData(VK_ESCAPE)]
        [InlineData(VK_SPACE)]
        [InlineData(VK_TAB)]
        [InlineData(VK_F)]
        public void TransitionKeys_GetTransitionDelay(int vk)
        {
            Assert.Equal(
                KeyDelayClassifier.TRANSITION_DELAY_MS,
                KeyDelayClassifier.DelayMsFor(vk));
        }

        // Tab-cycle keys (Q/E on PartyMenu, A/D on equipment pickers)
        // trigger a full re-render of a new tab. Treat as transition.
        [Theory]
        [InlineData(VK_Q)]
        [InlineData(VK_E)]
        [InlineData(VK_A)]
        [InlineData(VK_D)]
        public void TabCycleKeys_GetTransitionDelay(int vk)
        {
            Assert.Equal(
                KeyDelayClassifier.TRANSITION_DELAY_MS,
                KeyDelayClassifier.DelayMsFor(vk));
        }

        [Fact]
        public void NavDelay_IsFasterThanTransitionDelay()
        {
            Assert.True(KeyDelayClassifier.NAV_DELAY_MS < KeyDelayClassifier.TRANSITION_DELAY_MS,
                "Nav delay must be shorter than transition delay — that's the whole point of the split.");
        }

        [Fact]
        public void UnknownVk_FallsBackToTransitionDelay()
        {
            // Unknown VKs could be anything; err on the side of safety and use
            // the longer delay rather than risk dropping a key on a screen
            // we haven't classified.
            Assert.Equal(
                KeyDelayClassifier.TRANSITION_DELAY_MS,
                KeyDelayClassifier.DelayMsFor(0xFF));
        }

        // Edge cases (session 33 batch 7).

        [Fact]
        public void NavDelay_IsPositive()
        {
            Assert.True(KeyDelayClassifier.NAV_DELAY_MS > 0);
        }

        [Fact]
        public void TransitionDelay_IsPositive()
        {
            Assert.True(KeyDelayClassifier.TRANSITION_DELAY_MS > 0);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public void OutOfRange_VkCodes_FallBackToTransitionDelay(int vk)
        {
            Assert.Equal(
                KeyDelayClassifier.TRANSITION_DELAY_MS,
                KeyDelayClassifier.DelayMsFor(vk));
        }

        [Fact]
        public void OnlyArrowKeys_GetNavDelay()
        {
            // Sweep 0x00..0xFF and confirm NAV_DELAY only fires for the 4 arrow VKs.
            int navCount = 0;
            for (int vk = 0; vk <= 0xFF; vk++)
            {
                if (KeyDelayClassifier.DelayMsFor(vk) == KeyDelayClassifier.NAV_DELAY_MS)
                    navCount++;
            }
            Assert.Equal(4, navCount);
        }
    }
}
