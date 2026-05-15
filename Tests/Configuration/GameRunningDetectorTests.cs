using Xunit;
using FluentAssertions;
using FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Tests
{
    public class GameRunningDetectorTests
    {
        [Fact]
        public void IsGameRunning_ReturnsFalse_WhenNoSupportedProcessIsLive()
        {
            // Probe says nothing is running.
            var result = GameRunningDetector.IsGameRunning(name => false);

            result.Should().BeFalse();
        }

        [Fact]
        public void IsGameRunning_ReturnsTrue_WhenFftEnhancedIsLive()
        {
            var result = GameRunningDetector.IsGameRunning(name => name == "fft_enhanced");

            result.Should().BeTrue();
        }

        [Fact]
        public void IsGameRunning_ReturnsTrue_WhenFftClassicIsLive()
        {
            var result = GameRunningDetector.IsGameRunning(name => name == "fft_classic");

            result.Should().BeTrue();
        }

        [Fact]
        public void IsGameRunning_ReturnsFalse_WhenUnrelatedProcessIsLive()
        {
            var result = GameRunningDetector.IsGameRunning(name => name == "notepad");

            result.Should().BeFalse();
        }

        [Fact]
        public void SupportedProcessNames_MatchesModConfigSupportedAppIds()
        {
            // Guards against drift between ModConfig.json's SupportedAppId entries
            // and the names we probe for. If a new exe is supported, both lists
            // must move together.
            GameRunningDetector.SupportedProcessNames
                .Should().BeEquivalentTo(new[] { "fft_enhanced", "fft_classic" });
        }

        [Fact]
        public void IsGameRunning_NullProbe_ReturnsFalse()
        {
            var result = GameRunningDetector.IsGameRunning(null!);

            result.Should().BeFalse();
        }
    }
}
