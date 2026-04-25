using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Phoenix Down (and any "Removes KO" ability) reverses life state, so
    /// it's NOT just a revive — on Undead-status enemies it's an instant
    /// KO. The shell renderer needs to disambiguate these intent cases so
    /// the agent picks the right tile:
    ///
    ///   REVIVE          — dead ally, the canonical use
    ///   REVIVE-ENEMY    — dead enemy (USING THIS RESURRECTS THEM, usually bad)
    ///   KO              — undead-status enemy, the kill-via-reverse-revive move
    ///   KO-ALLY         — undead-status ally (kills your own undead unit)
    ///   none            — alive non-undead, no effect
    /// </summary>
    public class ReviveTargetClassifierTests
    {
        // Statuses byte 0 bit 0x10 = Undead per StatusDecoder
        private static readonly byte[] UndeadOnly = { 0x10, 0, 0, 0, 0 };
        private static readonly byte[] DeadOnly   = { 0x20, 0, 0, 0, 0 };
        private static readonly byte[] AliveBlank = { 0, 0, 0, 0, 0 };

        [Fact]
        public void DeadAlly_ReturnsRevive()
        {
            var intent = ReviveTargetClassifier.Classify(
                targetTeam: 0, casterTeam: 0,
                targetHp: 0, targetStatusBytes: DeadOnly);
            Assert.Equal(ReviveIntent.Revive, intent);
        }

        [Fact]
        public void DeadGuestAlly_Team2_ReturnsRevive()
        {
            // Team 2 (NPC guests like Agrias / Gaffgarion) count as allies.
            var intent = ReviveTargetClassifier.Classify(
                targetTeam: 2, casterTeam: 0,
                targetHp: 0, targetStatusBytes: DeadOnly);
            Assert.Equal(ReviveIntent.Revive, intent);
        }

        [Fact]
        public void DeadEnemy_ReturnsReviveEnemy()
        {
            // Phoenix Down on a dead enemy resurrects them as alive — usually
            // a bad call. Surface as a distinct intent so the agent knows.
            var intent = ReviveTargetClassifier.Classify(
                targetTeam: 1, casterTeam: 0,
                targetHp: 0, targetStatusBytes: DeadOnly);
            Assert.Equal(ReviveIntent.ReviveEnemy, intent);
        }

        [Fact]
        public void UndeadEnemy_ReturnsKo()
        {
            // The kill move — PD on an undead-status alive enemy reverses
            // their state and kills them outright.
            var intent = ReviveTargetClassifier.Classify(
                targetTeam: 1, casterTeam: 0,
                targetHp: 100, targetStatusBytes: UndeadOnly);
            Assert.Equal(ReviveIntent.Ko, intent);
        }

        [Fact]
        public void UndeadAlly_ReturnsKoAlly()
        {
            // Rare but possible: an ally unit hit with Undead via debuff.
            // PD on them KILLS them. Tag it differently so the agent doesn't
            // confuse it with the kill-an-enemy-undead use case.
            var intent = ReviveTargetClassifier.Classify(
                targetTeam: 0, casterTeam: 0,
                targetHp: 100, targetStatusBytes: UndeadOnly);
            Assert.Equal(ReviveIntent.KoAlly, intent);
        }

        [Fact]
        public void AliveNonUndead_ReturnsNone()
        {
            var intent = ReviveTargetClassifier.Classify(
                targetTeam: 1, casterTeam: 0,
                targetHp: 500, targetStatusBytes: AliveBlank);
            Assert.Equal(ReviveIntent.None, intent);
        }

        [Fact]
        public void DeadAndUndead_PrefersDeadIntent()
        {
            // If somehow both dead AND undead bits are set (shouldn't happen
            // in practice — game clears Undead on KO), the dead-state wins
            // since it's the more authoritative life-state signal and
            // matches what the game actually does (reviveable corpse).
            var deadAndUndead = new byte[] { 0x30, 0, 0, 0, 0 };
            var intent = ReviveTargetClassifier.Classify(
                targetTeam: 0, casterTeam: 0,
                targetHp: 0, targetStatusBytes: deadAndUndead);
            Assert.Equal(ReviveIntent.Revive, intent);
        }

        [Fact]
        public void NullStatusBytes_HandledGracefully()
        {
            var intent = ReviveTargetClassifier.Classify(
                targetTeam: 1, casterTeam: 0,
                targetHp: 100, targetStatusBytes: null);
            Assert.Equal(ReviveIntent.None, intent);
        }
    }
}
