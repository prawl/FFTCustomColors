using FFTColorCustomizer.GameBridge;
using System.Linq;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for <see cref="BuffPlanner.PlanInvincibilityWrites"/> — the pure
    /// "what bytes should I write to make this unit invincible?" function
    /// behind the `cheat_mode_buff` bridge action. Session 47 part 5.
    ///
    /// Deliberate non-goals for the invincibility buff:
    ///   - Do NOT change level / exp. FFT enemy scaling is tied to player
    ///     levels; boosting Ramza's level makes subsequent battles harder,
    ///     which defeats the purpose of a dev-tool cheat.
    ///   - Do NOT change brave / faith. Those are permanent stat changes
    ///     that persist to the roster copy after battle.
    ///
    /// In scope:
    ///   - HP / MaxHP at +0x14 / +0x16 (u16 each).
    ///   - PA at +0x22 (u8).
    ///   - Element affinity absorb byte at +0x5A (set to 0xFF = absorb all).
    ///   - Element weak / half / strengthen at +0x5C / +0x5B / +0x5E
    ///     zeroed so NOTHING amplifies incoming damage.
    /// </summary>
    public class BuffPlannerTests
    {
        [Fact]
        public void InvincibilityPlan_WritesSixOffsets()
        {
            var writes = BuffPlanner.PlanInvincibilityWrites(slotBase: 0x140894000L);
            // HP (u16), MaxHP (u16), PA (u8), Absorb (u8), Cancel (u8),
            // Half (u8), Weak (u8), Strengthen (u8) = 8 writes.
            Assert.Equal(8, writes.Count);
        }

        [Fact]
        public void HP_WrittenAsU16_AtPlus0x14()
        {
            var writes = BuffPlanner.PlanInvincibilityWrites(slotBase: 0x140894000L);
            var hp = writes.Single(w => w.Address == 0x140894014L);
            Assert.Equal(2, hp.Bytes.Length);
            // 999 = 0x03E7 little-endian → E7 03.
            Assert.Equal(0xE7, hp.Bytes[0]);
            Assert.Equal(0x03, hp.Bytes[1]);
        }

        [Fact]
        public void MaxHP_WrittenAsU16_AtPlus0x16()
        {
            var writes = BuffPlanner.PlanInvincibilityWrites(slotBase: 0x140894000L);
            var maxHp = writes.Single(w => w.Address == 0x140894016L);
            Assert.Equal(2, maxHp.Bytes.Length);
            Assert.Equal(0xE7, maxHp.Bytes[0]);
            Assert.Equal(0x03, maxHp.Bytes[1]);
        }

        [Fact]
        public void PA_WrittenAsU8_AtPlus0x22()
        {
            var writes = BuffPlanner.PlanInvincibilityWrites(slotBase: 0x140894000L);
            var pa = writes.Single(w => w.Address == 0x140894022L);
            Assert.Single(pa.Bytes);
            Assert.Equal(0xFF, pa.Bytes[0]);
        }

        [Fact]
        public void AbsorbAllElements_AtPlus0x5A()
        {
            // +0x5A is the Absorb mask. Setting to 0xFF means every element
            // (Fire/Lightning/Ice/Wind/Earth/Water/Holy/Dark) heals instead
            // of damaging. No damage source can hurt Ramza.
            var writes = BuffPlanner.PlanInvincibilityWrites(slotBase: 0x140894000L);
            var absorb = writes.Single(w => w.Address == 0x14089405AL);
            Assert.Single(absorb.Bytes);
            Assert.Equal(0xFF, absorb.Bytes[0]);
        }

        [Fact]
        public void WeakStrengthen_Zeroed_SoNothingAmplifies()
        {
            // +0x5B = Cancel, +0x5C = Half, +0x5D = Weak, +0x5E = Strengthen.
            // Absorb takes precedence (all elements) so the others are
            // effectively moot — but zero them out so a partial write
            // doesn't leave a Weak flag set from prior battles.
            var writes = BuffPlanner.PlanInvincibilityWrites(slotBase: 0x140894000L);
            Assert.Equal(0x00, writes.Single(w => w.Address == 0x14089405BL).Bytes[0]); // Cancel
            Assert.Equal(0x00, writes.Single(w => w.Address == 0x14089405CL).Bytes[0]); // Half
            Assert.Equal(0x00, writes.Single(w => w.Address == 0x14089405DL).Bytes[0]); // Weak
            Assert.Equal(0x00, writes.Single(w => w.Address == 0x14089405EL).Bytes[0]); // Strengthen
        }

        [Fact]
        public void DoesNotWrite_Level()
        {
            // Level is at +0x0D. Don't touch it — enemy scaling depends
            // on party levels.
            var writes = BuffPlanner.PlanInvincibilityWrites(slotBase: 0x140894000L);
            Assert.DoesNotContain(writes, w => w.Address == 0x14089400DL);
        }

        [Fact]
        public void DoesNotWrite_Exp()
        {
            // Exp at +0x0C. Leave alone.
            var writes = BuffPlanner.PlanInvincibilityWrites(slotBase: 0x140894000L);
            Assert.DoesNotContain(writes, w => w.Address == 0x14089400CL);
        }

        [Fact]
        public void DoesNotWrite_BraveOrFaith()
        {
            // Brave/Faith at +0x0E/+0x10. Permanent stat changes — don't
            // touch for a per-battle buff.
            var writes = BuffPlanner.PlanInvincibilityWrites(slotBase: 0x140894000L);
            Assert.DoesNotContain(writes, w => w.Address == 0x14089400EL);
            Assert.DoesNotContain(writes, w => w.Address == 0x140894010L);
        }

        [Fact]
        public void DoesNotWrite_Position()
        {
            // Grid x/y at +0x33/+0x34. Not touching — Ramza stays where
            // he spawned.
            var writes = BuffPlanner.PlanInvincibilityWrites(slotBase: 0x140894000L);
            Assert.DoesNotContain(writes, w => w.Address == 0x140894033L);
            Assert.DoesNotContain(writes, w => w.Address == 0x140894034L);
        }

        [Fact]
        public void DifferentSlotBase_AddressesShift()
        {
            // Sanity: addresses relative to the slot base, so a different
            // slot gets the same offsets at a different base.
            var writes = BuffPlanner.PlanInvincibilityWrites(slotBase: 0x140893C00L);
            var hp = writes.Single(w => w.Address == 0x140893C14L);
            Assert.NotNull(hp);
        }

        [Fact]
        public void HpValue_Configurable()
        {
            // 999 is the default; callers may want 9999. Plan respects it.
            var writes = BuffPlanner.PlanInvincibilityWrites(
                slotBase: 0x140894000L, hp: 9999);
            var hp = writes.Single(w => w.Address == 0x140894014L);
            // 9999 = 0x270F → 0F 27
            Assert.Equal(0x0F, hp.Bytes[0]);
            Assert.Equal(0x27, hp.Bytes[1]);
        }
    }
}
