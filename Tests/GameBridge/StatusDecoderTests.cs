using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class StatusDecoderTests
    {
        [Fact]
        public void Decode_AllZeros_ReturnsEmptyList()
        {
            var statuses = StatusDecoder.Decode(new byte[] { 0, 0, 0, 0, 0 });
            Assert.Empty(statuses);
        }

        [Fact]
        public void Decode_Poison_ReturnsPoisonOnly()
        {
            // Byte 4 bit 0x80 = Poison
            var statuses = StatusDecoder.Decode(new byte[] { 0, 0, 0, 0x80, 0 });
            Assert.Single(statuses);
            Assert.Contains("Poison", statuses);
        }

        [Fact]
        public void Decode_MultipleStatuses_ReturnsAll()
        {
            // Haste (byte4 0x08) + Protect (byte4 0x20) + Regen (byte4 0x40)
            var statuses = StatusDecoder.Decode(new byte[] { 0, 0, 0, 0x68, 0 });
            Assert.Equal(3, statuses.Count);
            Assert.Contains("Haste", statuses);
            Assert.Contains("Protect", statuses);
            Assert.Contains("Regen", statuses);
        }

        [Fact]
        public void Decode_Dead_ReturnsDead()
        {
            // Byte 1 bit 0x20 = Dead
            var statuses = StatusDecoder.Decode(new byte[] { 0x20, 0, 0, 0, 0 });
            Assert.Single(statuses);
            Assert.Contains("Dead", statuses);
        }

        [Fact]
        public void Decode_MixedAcrossBytes_ReturnsAll()
        {
            // Charging (byte1 0x08) + Silence (byte2 0x08) + Float (byte3 0x40) + Sleep (byte5 0x10)
            var statuses = StatusDecoder.Decode(new byte[] { 0x08, 0x08, 0x40, 0, 0x10 });
            Assert.Equal(4, statuses.Count);
            Assert.Contains("Charging", statuses);
            Assert.Contains("Silence", statuses);
            Assert.Contains("Float", statuses);
            Assert.Contains("Sleep", statuses);
        }

        [Fact]
        public void Decode_NullInput_ReturnsEmptyList()
        {
            var statuses = StatusDecoder.Decode(null!);
            Assert.Empty(statuses);
        }

        [Fact]
        public void Decode_TooShortInput_ReturnsEmptyList()
        {
            var statuses = StatusDecoder.Decode(new byte[] { 0xFF, 0xFF });
            Assert.Empty(statuses);
        }

        [Fact]
        public void Decode_ProtectAndShell_ReturnsBoth()
        {
            // Byte 4: Protect=0x20 + Shell=0x10 = 0x30
            var statuses = StatusDecoder.Decode(new byte[] { 0, 0, 0, 0x30, 0 });
            Assert.Equal(2, statuses.Count);
            Assert.Contains("Protect", statuses);
            Assert.Contains("Shell", statuses);
        }
        [Fact]
        public void GetLifeState_NoStatuses_ReturnsAlive()
        {
            Assert.Equal("alive", StatusDecoder.GetLifeState(new byte[] { 0, 0, 0, 0, 0 }));
        }

        [Fact]
        public void GetLifeState_Dead_ReturnsDead()
        {
            // Dead = byte 0, mask 0x20. Unit can be raised.
            Assert.Equal("dead", StatusDecoder.GetLifeState(new byte[] { 0x20, 0, 0, 0, 0 }));
        }

        [Fact]
        public void GetLifeState_Crystal_ReturnsCrystal()
        {
            // Crystal = byte 0, mask 0x40. Permanently gone.
            Assert.Equal("crystal", StatusDecoder.GetLifeState(new byte[] { 0x40, 0, 0, 0, 0 }));
        }

        [Fact]
        public void GetLifeState_Treasure_ReturnsTreasure()
        {
            // Treasure = byte 1, mask 0x01. Permanently gone.
            Assert.Equal("treasure", StatusDecoder.GetLifeState(new byte[] { 0, 0x01, 0, 0, 0 }));
        }

        [Fact]
        public void GetLifeState_DeadAndCrystal_ReturnsCrystal()
        {
            // Crystal takes priority over Dead (crystal implies dead already)
            Assert.Equal("crystal", StatusDecoder.GetLifeState(new byte[] { 0x60, 0, 0, 0, 0 }));
        }

        [Fact]
        public void GetLifeState_DeadWithOtherStatuses_ReturnsDead()
        {
            // Dead + Poison + Haste — still dead, other statuses don't matter
            Assert.Equal("dead", StatusDecoder.GetLifeState(new byte[] { 0x20, 0, 0, 0x88, 0 }));
        }

        [Fact]
        public void GetLifeState_NullBytes_ReturnsAlive()
        {
            Assert.Equal("alive", StatusDecoder.GetLifeState(null!));
        }
    }
}
