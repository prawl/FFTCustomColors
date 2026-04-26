using System.Collections.Generic;
using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class ActionBlockingStatusClassifierTests
    {
        [Fact]
        public void Empty_ReturnsNull()
        {
            Assert.Null(ActionBlockingStatusClassifier.GetBlockingTag(new List<string>()));
        }

        [Fact]
        public void Null_ReturnsNull()
        {
            Assert.Null(ActionBlockingStatusClassifier.GetBlockingTag(null));
        }

        [Fact]
        public void OnlyBuffs_ReturnsNull()
        {
            // Buffs / non-blocking statuses don't trigger the tag.
            var statuses = new List<string> { "Haste", "Protect", "Shell", "Regen" };
            Assert.Null(ActionBlockingStatusClassifier.GetBlockingTag(statuses));
        }

        [Fact]
        public void DontAct_ReturnsDontActTag()
        {
            var statuses = new List<string> { "DontAct" };
            var tag = ActionBlockingStatusClassifier.GetBlockingTag(statuses);
            Assert.Equal("DontAct(no act)", tag);
        }

        [Fact]
        public void DontMove_ReturnsDontMoveTag()
        {
            var statuses = new List<string> { "DontMove" };
            var tag = ActionBlockingStatusClassifier.GetBlockingTag(statuses);
            Assert.Equal("DontMove(no move)", tag);
        }

        [Fact]
        public void Sleep_ReturnsSleepTag()
        {
            var statuses = new List<string> { "Sleep" };
            var tag = ActionBlockingStatusClassifier.GetBlockingTag(statuses);
            Assert.Equal("Sleep(sleeping)", tag);
        }

        [Fact]
        public void Petrify_ReturnsPetrifyTag()
        {
            var statuses = new List<string> { "Petrify" };
            var tag = ActionBlockingStatusClassifier.GetBlockingTag(statuses);
            Assert.Equal("Petrify(frozen)", tag);
        }

        [Fact]
        public void Stop_ReturnsStopTag()
        {
            Assert.Equal("Stop(frozen)", ActionBlockingStatusClassifier.GetBlockingTag(new List<string> { "Stop" }));
        }

        [Fact]
        public void Frog_ReturnsFrogTag()
        {
            Assert.Equal("Frog(transformed)", ActionBlockingStatusClassifier.GetBlockingTag(new List<string> { "Frog" }));
        }

        [Fact]
        public void Charm_ReturnsCharmTag()
        {
            Assert.Equal("Charm(charmed)", ActionBlockingStatusClassifier.GetBlockingTag(new List<string> { "Charm" }));
        }

        [Fact]
        public void Confusion_ReturnsConfusionTag()
        {
            Assert.Equal("Confusion(confused)", ActionBlockingStatusClassifier.GetBlockingTag(new List<string> { "Confusion" }));
        }

        [Fact]
        public void Berserk_ReturnsBerserkTag()
        {
            Assert.Equal("Berserk(auto-attack)", ActionBlockingStatusClassifier.GetBlockingTag(new List<string> { "Berserk" }));
        }

        [Fact]
        public void Mixed_BlockingStatusWithBuffs_ReturnsBlockingTag()
        {
            // The tag picks the action-blocking status; buffs are ignored.
            var statuses = new List<string> { "Haste", "DontAct", "Regen" };
            Assert.Equal("DontAct(no act)", ActionBlockingStatusClassifier.GetBlockingTag(statuses));
        }

        [Fact]
        public void MultipleBlocking_PrefersHighestSeverity()
        {
            // Petrify > Sleep > DontAct in severity. Live-flagged playtest #9
            // 2026-04-26 had Wilham at DontAct alone; with multiple blockers
            // the most-restrictive wins so the agent doesn't try to wait+heal
            // on a Petrified unit.
            var statuses = new List<string> { "DontAct", "Sleep", "Petrify" };
            Assert.Equal("Petrify(frozen)", ActionBlockingStatusClassifier.GetBlockingTag(statuses));
        }

        [Fact]
        public void DontMove_WithDontAct_PrefersDontAct()
        {
            // DontAct is more restrictive (can't ability) than DontMove (can
            // still ability from current tile); surface DontAct.
            var statuses = new List<string> { "DontMove", "DontAct" };
            Assert.Equal("DontAct(no act)", ActionBlockingStatusClassifier.GetBlockingTag(statuses));
        }

        [Fact]
        public void CaseInsensitive_StatusNameMatching()
        {
            // The bridge writes "DontAct" but defensive code shouldn't choke
            // on lowercase or mixed case.
            var statuses = new List<string> { "dontact" };
            Assert.Equal("DontAct(no act)", ActionBlockingStatusClassifier.GetBlockingTag(statuses));
        }
    }
}
