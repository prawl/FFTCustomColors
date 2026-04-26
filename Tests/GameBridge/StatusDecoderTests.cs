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

        [Fact]
        public void GetLifeState_Petrify_ReturnsPetrified()
        {
            // Petrify = byte 1, mask 0x80. Per S60 live-verified behavior:
            // a petrified unit can't act and can't be attacked — effectively
            // KO'd until the status is removed (Gold Needle). Treated the
            // same as "dead" for targeting + battle-end filtering.
            Assert.Equal("petrified", StatusDecoder.GetLifeState(new byte[] { 0, 0x80, 0, 0, 0 }));
        }

        [Fact]
        public void GetLifeState_PetrifyWithOtherStatuses_ReturnsPetrified()
        {
            // Petrify + Float (Bomb that got stoned) — still petrified
            Assert.Equal("petrified", StatusDecoder.GetLifeState(new byte[] { 0, 0x80, 0x40, 0, 0 }));
        }

        [Fact]
        public void GetLifeState_DeadAndPetrify_DeadWinsByPriority()
        {
            // Guard: if both bits are set (shouldn't happen in practice),
            // "dead" takes precedence over "petrified" — dead = already KO'd
            // so the raise-able variant is the most accurate state.
            Assert.Equal("dead", StatusDecoder.GetLifeState(new byte[] { 0x20, 0x80, 0, 0, 0 }));
        }

        // Additional coverage (session 33 batch 6).

        [Fact]
        public void Decode_AllZeros_ReturnsEmpty()
        {
            Assert.Empty(StatusDecoder.Decode(new byte[] { 0, 0, 0, 0, 0 }));
        }

        [Fact]
        public void Decode_NullInput_ReturnsEmpty()
        {
            Assert.Empty(StatusDecoder.Decode(null!));
        }

        [Fact]
        public void Decode_TooShortInput_ReturnsEmpty()
        {
            // Fewer than 5 bytes — decoder should handle gracefully, not throw.
            Assert.Empty(StatusDecoder.Decode(new byte[] { 0x80 }));
            Assert.Empty(StatusDecoder.Decode(new byte[] { 0x80, 0x80 }));
        }

        [Theory]
        [InlineData(3, 0x80, "Poison")]
        [InlineData(3, 0x40, "Regen")]
        [InlineData(3, 0x20, "Protect")]
        [InlineData(3, 0x10, "Shell")]
        [InlineData(3, 0x08, "Haste")]
        [InlineData(4, 0x80, "Faith")]
        [InlineData(4, 0x10, "Sleep")]
        [InlineData(4, 0x02, "Reflect")]
        public void Decode_SingleStatusBit_ReturnsThatStatus(int byteIdx, byte mask, string expected)
        {
            var bytes = new byte[5];
            bytes[byteIdx] = mask;
            var result = StatusDecoder.Decode(bytes);
            Assert.Single(result);
            Assert.Equal(expected, result[0]);
        }

        [Fact]
        public void Decode_AllBitsSet_ReturnsManyStatuses()
        {
            // 5 bytes of 0xFF — ~40 bits set but some are reserved (not mapped).
            // We just verify we get a non-trivial count and no crashes.
            var result = StatusDecoder.Decode(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });
            Assert.True(result.Count >= 35, $"expected ≥35 statuses on all-FF, got {result.Count}");
        }

        [Fact]
        public void Decode_EveryBitMapped_YieldsUniqueName()
        {
            // No two bit positions map to the same status name.
            var names = new HashSet<string>();
            for (int b = 0; b < 5; b++)
            {
                for (int bit = 0; bit < 8; bit++)
                {
                    var bytes = new byte[5];
                    bytes[b] = (byte)(1 << bit);
                    var result = StatusDecoder.Decode(bytes);
                    if (result.Count == 1)
                    {
                        Assert.True(names.Add(result[0]),
                            $"duplicate status name '{result[0]}' at byte {b} bit {bit}");
                    }
                }
            }
        }

        [Fact]
        public void GetLifeState_TooShortInput_ReturnsAlive()
        {
            // 1-byte input — decoder needs 2 to determine life state.
            Assert.Equal("alive", StatusDecoder.GetLifeState(new byte[] { 0x20 }));
        }

        [Fact]
        public void GetLifeState_EmptyArray_ReturnsAlive()
        {
            Assert.Equal("alive", StatusDecoder.GetLifeState(System.Array.Empty<byte>()));
        }

        [Fact]
        public void Decode_CommonStatusCombination_CureAndPoison()
        {
            // A unit mid-combat: Poison + Haste + Protect (byte 3: 0x80|0x08|0x20 = 0xA8).
            var result = StatusDecoder.Decode(new byte[] { 0, 0, 0, 0xA8, 0 });
            Assert.Contains("Poison", result);
            Assert.Contains("Haste", result);
            Assert.Contains("Protect", result);
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void Decode_CrystalStatus_IsInResult()
        {
            // Crystal bit set (byte 0 mask 0x40).
            var result = StatusDecoder.Decode(new byte[] { 0x40, 0, 0, 0, 0 });
            Assert.Contains("Crystal", result);
        }

        // DecodeAliveStatuses: returns Decode() but EXCLUDES the four
        // entries that are surfaced separately as `lifeState` (Crystal /
        // Dead / Treasure / Petrify). Without this filter the rendered
        // unit row carried both `[Treasure]` and ` TREASURE` (or worse:
        // the same word inside `[...]` collided visually with alive
        // statuses like Defending and Charging — playtest #7 friction).

        [Fact]
        public void DecodeAliveStatuses_NoStatuses_ReturnsEmpty()
        {
            Assert.Empty(StatusDecoder.DecodeAliveStatuses(new byte[] { 0, 0, 0, 0, 0 }));
        }

        [Fact]
        public void DecodeAliveStatuses_OnlyDead_ReturnsEmpty()
        {
            // Dead is a lifeState, not an alive status — filtered out.
            Assert.Empty(StatusDecoder.DecodeAliveStatuses(new byte[] { 0x20, 0, 0, 0, 0 }));
        }

        [Fact]
        public void DecodeAliveStatuses_OnlyCrystal_ReturnsEmpty()
        {
            Assert.Empty(StatusDecoder.DecodeAliveStatuses(new byte[] { 0x40, 0, 0, 0, 0 }));
        }

        [Fact]
        public void DecodeAliveStatuses_OnlyTreasure_ReturnsEmpty()
        {
            Assert.Empty(StatusDecoder.DecodeAliveStatuses(new byte[] { 0, 0x01, 0, 0, 0 }));
        }

        [Fact]
        public void DecodeAliveStatuses_OnlyPetrify_ReturnsEmpty()
        {
            Assert.Empty(StatusDecoder.DecodeAliveStatuses(new byte[] { 0, 0x80, 0, 0, 0 }));
        }

        [Fact]
        public void DecodeAliveStatuses_DeadPlusBuffs_KeepsBuffs()
        {
            // Dead + Regen + Protect + Shell — playtest #7 saw these on Ramza's
            // corpse. Surfacing the buffs is useful (tells you what he had
            // when KO'd); the lifeState gets the DEAD suffix separately.
            var result = StatusDecoder.DecodeAliveStatuses(new byte[] { 0x20, 0, 0, 0x70, 0 });
            Assert.Equal(3, result.Count);
            Assert.Contains("Regen", result);
            Assert.Contains("Protect", result);
            Assert.Contains("Shell", result);
            Assert.DoesNotContain("Dead", result);
        }

        [Fact]
        public void DecodeAliveStatuses_NoLifeState_ReturnsAllStatuses()
        {
            // No life-state bits — behavior matches Decode() exactly.
            var result = StatusDecoder.DecodeAliveStatuses(new byte[] { 0, 0, 0, 0xA8, 0 });
            Assert.Equal(3, result.Count);
            Assert.Contains("Poison", result);
            Assert.Contains("Haste", result);
            Assert.Contains("Protect", result);
        }

        [Fact]
        public void DecodeAliveStatuses_NullInput_ReturnsEmpty()
        {
            Assert.Empty(StatusDecoder.DecodeAliveStatuses(null!));
        }
    }
}
