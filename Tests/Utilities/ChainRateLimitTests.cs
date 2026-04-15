using System;
using Xunit;
using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.Tests
{
    /// <summary>
    /// Unit tests for <see cref="CommandWatcher.ComputeChainDelay"/> — the pure
    /// helper that decides whether a newly-arrived game command should be
    /// auto-delayed because a previous game command finished too recently.
    /// The bridge needs this to defeat `&&`-chained shell calls that race the
    /// game's key-input handler during menu-open animations.
    /// </summary>
    public class ChainRateLimitTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void NoPreviousCommand_NoDelay()
        {
            var (sleepMs, warning) = CommandWatcher.ComputeChainDelay(
                isObservational: false,
                lastCommandCompletedAt: DateTime.MinValue,
                now: T0,
                floorMs: 250);

            Assert.Equal(0, sleepMs);
            Assert.Null(warning);
        }

        [Fact]
        public void ObservationalCommand_NeverDelayed()
        {
            var (sleepMs, warning) = CommandWatcher.ComputeChainDelay(
                isObservational: true,
                lastCommandCompletedAt: T0,
                now: T0.AddMilliseconds(5),  // 5ms later — would normally trigger
                floorMs: 250);

            Assert.Equal(0, sleepMs);
            Assert.Null(warning);
        }

        [Fact]
        public void GameCommandWithinFloor_Delayed()
        {
            var (sleepMs, warning) = CommandWatcher.ComputeChainDelay(
                isObservational: false,
                lastCommandCompletedAt: T0,
                now: T0.AddMilliseconds(100),
                floorMs: 250);

            Assert.Equal(150, sleepMs);
            Assert.NotNull(warning);
            Assert.Contains("auto-delayed 150ms", warning);
            Assert.Contains("prev game command 100ms ago", warning);
            Assert.Contains("floor=250ms", warning);
            Assert.Contains("keys:[...]", warning);
        }

        [Fact]
        public void GameCommandExactlyAtFloor_NoDelay()
        {
            var (sleepMs, warning) = CommandWatcher.ComputeChainDelay(
                isObservational: false,
                lastCommandCompletedAt: T0,
                now: T0.AddMilliseconds(250),
                floorMs: 250);

            Assert.Equal(0, sleepMs);
            Assert.Null(warning);
        }

        [Fact]
        public void GameCommandAfterFloor_NoDelay()
        {
            var (sleepMs, warning) = CommandWatcher.ComputeChainDelay(
                isObservational: false,
                lastCommandCompletedAt: T0,
                now: T0.AddMilliseconds(500),
                floorMs: 250);

            Assert.Equal(0, sleepMs);
            Assert.Null(warning);
        }

        [Fact]
        public void VeryRapidChain_DelaysNearlyFullFloor()
        {
            // elapsed = 1ms → sleep should round up to 249ms
            var (sleepMs, warning) = CommandWatcher.ComputeChainDelay(
                isObservational: false,
                lastCommandCompletedAt: T0,
                now: T0.AddMilliseconds(1),
                floorMs: 250);

            Assert.Equal(249, sleepMs);
            Assert.NotNull(warning);
        }

        [Fact]
        public void ZeroElapsed_DelaysFullFloor()
        {
            var (sleepMs, warning) = CommandWatcher.ComputeChainDelay(
                isObservational: false,
                lastCommandCompletedAt: T0,
                now: T0,
                floorMs: 250);

            Assert.Equal(250, sleepMs);
            Assert.NotNull(warning);
        }

        [Fact]
        public void SubMillisecondElapsed_CeilsUp()
        {
            // 0.5ms elapsed → remaining = 249.5ms → Ceil → 250ms
            var (sleepMs, _) = CommandWatcher.ComputeChainDelay(
                isObservational: false,
                lastCommandCompletedAt: T0,
                now: T0.AddTicks(5000),  // 0.5ms in ticks
                floorMs: 250);

            Assert.Equal(250, sleepMs);
        }

        [Fact]
        public void WarningMentionsBatchFix()
        {
            // The warning must point Claude/humans at the fix: keys:[...] batch.
            // If this string ever changes, update fft.sh's banner text too.
            var (_, warning) = CommandWatcher.ComputeChainDelay(
                isObservational: false,
                lastCommandCompletedAt: T0,
                now: T0.AddMilliseconds(50),
                floorMs: 250);

            Assert.NotNull(warning);
            Assert.Contains("keys:[...]", warning);
            Assert.Contains("&&", warning);
        }
    }
}
