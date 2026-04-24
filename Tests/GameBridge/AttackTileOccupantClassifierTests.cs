using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Pin the "is this occupant attackable?" rule used by the Attack
    /// tiles renderer. A tile with an attackable occupant shows
    /// "enemy (Foo) HP=N"; an unattackable occupant renders as "empty"
    /// so Claude doesn't waste an action targeting a corpse. Dead,
    /// Crystal, Treasure, Petrified all disqualify; HP==0 without a
    /// status flag also disqualifies (animation transient defense).
    /// </summary>
    public class AttackTileOccupantClassifierTests
    {
        private static byte[] Status(int b0 = 0, int b1 = 0, int b2 = 0, int b3 = 0, int b4 = 0)
            => new byte[] { (byte)b0, (byte)b1, (byte)b2, (byte)b3, (byte)b4 };

        [Fact]
        public void AliveFullHp_IsAttackable()
        {
            Assert.True(AttackTileOccupantClassifier.IsAttackable(
                hp: 100, statusBytes: Status()));
        }

        [Fact]
        public void DeadFlagSet_NotAttackable()
        {
            // statusBytes[0] & 0x20 = dead bit
            Assert.False(AttackTileOccupantClassifier.IsAttackable(
                hp: 0, statusBytes: Status(b0: 0x20)));
        }

        [Fact]
        public void CrystalFlagSet_NotAttackable()
        {
            // statusBytes[0] & 0x40 = crystal bit
            Assert.False(AttackTileOccupantClassifier.IsAttackable(
                hp: 0, statusBytes: Status(b0: 0x40)));
        }

        [Fact]
        public void TreasureFlagSet_NotAttackable()
        {
            // statusBytes[1] & 0x01 = treasure bit
            Assert.False(AttackTileOccupantClassifier.IsAttackable(
                hp: 0, statusBytes: Status(b1: 0x01)));
        }

        [Fact]
        public void PetrifiedFlagSet_NotAttackable()
        {
            // statusBytes[1] & 0x80 = petrify bit
            Assert.False(AttackTileOccupantClassifier.IsAttackable(
                hp: 100, statusBytes: Status(b1: 0x80)));
        }

        [Fact]
        public void HpZero_WithoutDeadStatus_NotAttackable()
        {
            // Defense against animation-transient reads: HP=0 but the
            // dead status bit hasn't propagated yet. Would otherwise show
            // "enemy (Knight) HP=0" as an attack target. Live-repro TODO
            // 2026-04-24 — user flagged that a seemingly-dead Exploder
            // still appeared in Attack tiles.
            Assert.False(AttackTileOccupantClassifier.IsAttackable(
                hp: 0, statusBytes: Status()));
        }

        [Fact]
        public void NullStatusBytes_TreatsAsAlive_IfHpPositive()
        {
            Assert.True(AttackTileOccupantClassifier.IsAttackable(
                hp: 50, statusBytes: null));
        }

        [Fact]
        public void NullStatusBytes_HpZero_NotAttackable()
        {
            Assert.False(AttackTileOccupantClassifier.IsAttackable(
                hp: 0, statusBytes: null));
        }

        [Fact]
        public void NegativeHp_NotAttackable()
        {
            Assert.False(AttackTileOccupantClassifier.IsAttackable(
                hp: -1, statusBytes: Status()));
        }

        [Fact]
        public void ShortStatusBytes_TreatedAsAlive()
        {
            // StatusDecoder treats <2 bytes as "alive" (can't decode);
            // we mirror that by falling through to the HP check.
            Assert.True(AttackTileOccupantClassifier.IsAttackable(
                hp: 50, statusBytes: new byte[] { 0 }));
        }

        // -------- ClassifyOccupant --------

        [Fact]
        public void Classify_NoOccupant_Empty()
        {
            Assert.Equal("empty", AttackTileOccupantClassifier.ClassifyOccupant(
                hasOccupant: false, hp: 0, statusBytes: null, team: 0));
        }

        [Fact]
        public void Classify_AliveEnemy_ReturnsEnemy()
        {
            Assert.Equal("enemy", AttackTileOccupantClassifier.ClassifyOccupant(
                hasOccupant: true, hp: 100, statusBytes: Status(), team: 1));
        }

        [Fact]
        public void Classify_AlivePlayer_ReturnsAlly()
        {
            Assert.Equal("ally", AttackTileOccupantClassifier.ClassifyOccupant(
                hasOccupant: true, hp: 100, statusBytes: Status(), team: 0));
        }

        [Fact]
        public void Classify_DeadFlagSet_ReturnsDead()
        {
            Assert.Equal("dead", AttackTileOccupantClassifier.ClassifyOccupant(
                hasOccupant: true, hp: 0, statusBytes: Status(b0: 0x20), team: 1));
        }

        [Fact]
        public void Classify_CrystalFlagSet_ReturnsCrystal()
        {
            Assert.Equal("crystal", AttackTileOccupantClassifier.ClassifyOccupant(
                hasOccupant: true, hp: 0, statusBytes: Status(b0: 0x40), team: 1));
        }

        [Fact]
        public void Classify_TreasureFlagSet_ReturnsTreasure()
        {
            Assert.Equal("treasure", AttackTileOccupantClassifier.ClassifyOccupant(
                hasOccupant: true, hp: 0, statusBytes: Status(b1: 0x01), team: 1));
        }

        [Fact]
        public void Classify_PetrifiedFlagSet_ReturnsPetrified()
        {
            Assert.Equal("petrified", AttackTileOccupantClassifier.ClassifyOccupant(
                hasOccupant: true, hp: 100, statusBytes: Status(b1: 0x80), team: 1));
        }

        [Fact]
        public void Classify_HpZero_NoStatusFlag_ReturnsDeadFallback()
        {
            // HP dropped to 0 but the status byte hasn't propagated —
            // fall back to "dead" rather than collapse to "empty".
            Assert.Equal("dead", AttackTileOccupantClassifier.ClassifyOccupant(
                hasOccupant: true, hp: 0, statusBytes: Status(), team: 1));
        }

        [Fact]
        public void Classify_HpZero_NullStatusBytes_ReturnsDeadFallback()
        {
            Assert.Equal("dead", AttackTileOccupantClassifier.ClassifyOccupant(
                hasOccupant: true, hp: 0, statusBytes: null, team: 1));
        }
    }
}
