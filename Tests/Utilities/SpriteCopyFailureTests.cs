using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests
{
    /// <summary>
    /// CC-13: sprite-copy failures used to be demoted to DEBUG with the false promise
    /// "theme will be applied via path redirection" (no such redirection exists in
    /// production), silently dropping the user's change. The composed warning must tell
    /// the truth: the change was NOT applied, and what to do about it.
    /// </summary>
    public class SpriteCopyFailureTests
    {
        [Theory]
        [InlineData(SpriteCopyFailureKind.Locked)]
        [InlineData(SpriteCopyFailureKind.AccessDenied)]
        public void Warning_names_the_sprite_and_admits_the_change_was_not_applied(SpriteCopyFailureKind kind)
        {
            var msg = SpriteCopyFailure.Compose("battle_knight_m_spr.bin", kind);
            Assert.Contains("battle_knight_m_spr.bin", msg);
            Assert.Contains("NOT applied", msg);
        }

        [Fact]
        public void Warning_never_promises_path_redirection()
        {
            foreach (var kind in new[] { SpriteCopyFailureKind.Locked, SpriteCopyFailureKind.AccessDenied })
            {
                var msg = SpriteCopyFailure.Compose("x.bin", kind);
                Assert.DoesNotContain("redirection", msg);
            }
        }

        [Fact]
        public void Locked_and_access_denied_give_distinct_actionable_hints()
        {
            var locked = SpriteCopyFailure.Compose("x.bin", SpriteCopyFailureKind.Locked);
            var denied = SpriteCopyFailure.Compose("x.bin", SpriteCopyFailureKind.AccessDenied);
            Assert.NotEqual(locked, denied);
            Assert.Contains("restart", locked, System.StringComparison.OrdinalIgnoreCase);
            Assert.Contains("read-only", denied, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
