using FFTColorCustomizer.GameBridge;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Tests for <see cref="KillEnemiesPlanner.Plan"/> — the pure function
    /// behind the `cheat_kill_enemies` bridge action (session 49 rewrite).
    /// Master HP table discovery happens in the dispatcher; the planner
    /// just decides what to write given a table snapshot.
    /// </summary>
    public class KillEnemiesPlannerTests
    {
        private static KillEnemySlot Slot(long baseAddr, int hp, int maxHp, bool isPlayer = false)
            => new KillEnemySlot { SlotBase = baseAddr, Hp = hp, MaxHp = maxHp, IsPlayer = isPlayer };

        [Fact]
        public void EmptyInput_NoWrites()
        {
            var writes = KillEnemiesPlanner.Plan(new List<KillEnemySlot>());
            Assert.Empty(writes);
        }

        [Fact]
        public void SingleEnemy_WritesHpZeroAndDeadBit()
        {
            long baseAddr = 0x14184D8C0L;
            var writes = KillEnemiesPlanner.Plan(new[]
            {
                Slot(baseAddr, hp: 647, maxHp: 647)
            });
            Assert.Equal(2, writes.Count);
            var hp = writes.Single(w => w.Address == baseAddr + 0x00);
            Assert.Equal(new byte[] { 0x00, 0x00 }, hp.Bytes);
            var dead = writes.Single(w => w.Address == baseAddr + 0x31);
            Assert.Equal(new byte[] { 0x20 }, dead.Bytes);
        }

        [Fact]
        public void PlayerSlot_Skipped()
        {
            long baseAddr = 0x14184F8C0L;
            var writes = KillEnemiesPlanner.Plan(new[]
            {
                Slot(baseAddr, hp: 719, maxHp: 719, isPlayer: true)
            });
            Assert.Empty(writes);
        }

        [Fact]
        public void EmptySlot_Skipped_WhenMaxHpZero()
        {
            // Table has empty slots between enemies and players (MaxHp=0).
            var writes = KillEnemiesPlanner.Plan(new[]
            {
                Slot(0x14184E4C0L, hp: 0, maxHp: 0)
            });
            Assert.Empty(writes);
        }

        [Fact]
        public void AlreadyDead_Skipped_WhenHpZero()
        {
            // An enemy already at HP=0 doesn't need re-writing.
            // Saves memory calls and avoids re-setting a cleared dead bit.
            var writes = KillEnemiesPlanner.Plan(new[]
            {
                Slot(0x14184D8C0L, hp: 0, maxHp: 647)
            });
            Assert.Empty(writes);
        }

        [Fact]
        public void MultipleEnemies_TwoWritesEach()
        {
            // Session 49 Siedge Weald: 5 alive enemies (after one KO).
            var slots = new[]
            {
                Slot(0x14184D8C0L, hp: 59,  maxHp: 647),  // Skeleton
                Slot(0x14184DAC0L, hp: 668, maxHp: 668),  // enemy
                Slot(0x14184E0C0L, hp: 426, maxHp: 426),  // Black Goblin
                Slot(0x14184E2C0L, hp: 446, maxHp: 446),  // Black Goblin
                Slot(0x14184E4C0L, hp: 446, maxHp: 446),  // Black Goblin
            };
            var writes = KillEnemiesPlanner.Plan(slots);
            Assert.Equal(10, writes.Count);
            foreach (var s in slots)
            {
                Assert.Contains(writes, w => w.Address == s.SlotBase + 0x00);
                Assert.Contains(writes, w => w.Address == s.SlotBase + 0x31);
            }
        }

        [Fact]
        public void MixedSlots_OnlyLiveEnemiesWritten()
        {
            // Realistic table snapshot: 2 live enemies, 1 dead enemy, 2
            // empty slots, 1 player. Only the 2 live enemies produce writes.
            var slots = new[]
            {
                Slot(0x14184D8C0L, hp: 647, maxHp: 647),                 // live enemy
                Slot(0x14184DAC0L, hp: 0,   maxHp: 556),                 // dead enemy
                Slot(0x14184DCC0L, hp: 0,   maxHp: 0),                   // empty
                Slot(0x14184DEC0L, hp: 426, maxHp: 426),                 // live enemy
                Slot(0x14184E0C0L, hp: 0,   maxHp: 0),                   // empty
                Slot(0x14184F8C0L, hp: 719, maxHp: 719, isPlayer: true), // player
            };
            var writes = KillEnemiesPlanner.Plan(slots);
            Assert.Equal(4, writes.Count); // 2 enemies × 2 writes
            Assert.Contains(writes, w => w.Address == 0x14184D8C0L + 0x00);
            Assert.Contains(writes, w => w.Address == 0x14184D8C0L + 0x31);
            Assert.Contains(writes, w => w.Address == 0x14184DEC0L + 0x00);
            Assert.Contains(writes, w => w.Address == 0x14184DEC0L + 0x31);
        }

        [Fact]
        public void NullSlotInList_Skipped()
        {
            var writes = KillEnemiesPlanner.Plan(new KillEnemySlot[]
            {
                null!,
                Slot(0x14184D8C0L, hp: 647, maxHp: 647)
            });
            Assert.Equal(2, writes.Count); // only real slot counted
        }

        [Fact]
        public void NullInput_Empty()
        {
            var writes = KillEnemiesPlanner.Plan(null!);
            Assert.Empty(writes);
        }

        [Fact]
        public void BattleArraySlot_AddsReraiseClearWrite()
        {
            // Session 49: undead (Skeleton etc.) have Reraise auto-applied;
            // a plain HP=0 + dead-bit write lets them revive on turn rollover.
            // If the caller knows the battle-array slot, the planner emits
            // an additional write to clear the Reraise status bit at +0x47
            // bit 0x20 (confirmed live via Lloyd diff vs Ramza).
            long masterBase = 0x14184D8C0L;
            long battleArrayBase = 0x140892000L;
            var slot = new KillEnemySlot
            {
                SlotBase = masterBase,
                Hp = 647,
                MaxHp = 647,
                IsPlayer = false,
                BattleArraySlotBase = battleArrayBase,
                CurrentStatusByte2 = 0x20, // Reraise bit set
            };
            var writes = KillEnemiesPlanner.Plan(new[] { slot });
            // 3 writes: HP=0, dead-bit, Reraise-clear
            Assert.Equal(3, writes.Count);
            var reraise = writes.Single(w => w.Address == battleArrayBase + 0x47);
            // Reraise bit (0x20) cleared; keep all other bits (status byte
            // was 0x20 so result is 0x00).
            Assert.Equal(new byte[] { 0x00 }, reraise.Bytes);
        }

        [Fact]
        public void BattleArraySlot_PreservesOtherStatusBits()
        {
            // If the Reraise-holding unit also has other statuses (Regen=0x40
            // bit in byte 3 of a 5-byte field? Actually byte 2 holds Reraise
            // and other bits — test that we only clear 0x20 and keep 0x4F).
            long masterBase = 0x14184D8C0L;
            long battleArrayBase = 0x140892000L;
            var slot = new KillEnemySlot
            {
                SlotBase = masterBase,
                Hp = 647,
                MaxHp = 647,
                IsPlayer = false,
                BattleArraySlotBase = battleArrayBase,
                CurrentStatusByte2 = 0x2F, // Reraise + other bits
            };
            var writes = KillEnemiesPlanner.Plan(new[] { slot });
            var reraise = writes.Single(w => w.Address == battleArrayBase + 0x47);
            // Clear 0x20, keep 0x0F → 0x0F.
            Assert.Equal(new byte[] { 0x0F }, reraise.Bytes);
        }

        [Fact]
        public void BattleArraySlot_NoReraiseBitSet_NoExtraWrite()
        {
            // If Reraise isn't active, don't emit the clear-write. Saves
            // unnecessary memory operations.
            long masterBase = 0x14184D8C0L;
            long battleArrayBase = 0x140892000L;
            var slot = new KillEnemySlot
            {
                SlotBase = masterBase,
                Hp = 647,
                MaxHp = 647,
                IsPlayer = false,
                BattleArraySlotBase = battleArrayBase,
                CurrentStatusByte2 = 0x00, // no Reraise
            };
            var writes = KillEnemiesPlanner.Plan(new[] { slot });
            // 2 writes only (HP=0, dead-bit) — no Reraise-clear.
            Assert.Equal(2, writes.Count);
        }

        // ReviveAlliesPlanner tests (session 49 Task 30)

        [Fact]
        public void PlanReviveAllies_EmptyInput_NoWrites()
        {
            var writes = KillEnemiesPlanner.PlanReviveAllies(new List<KillEnemySlot>());
            Assert.Empty(writes);
        }

        [Fact]
        public void PlanReviveAllies_LivePlayer_NoWrites()
        {
            // Player already alive — no revive needed.
            var writes = KillEnemiesPlanner.PlanReviveAllies(new[]
            {
                new KillEnemySlot
                {
                    SlotBase = 0x14184F8C0L,
                    Hp = 500, MaxHp = 719, IsPlayer = true,
                }
            });
            Assert.Empty(writes);
        }

        [Fact]
        public void PlanReviveAllies_DeadPlayer_WritesHpFullAndClearsDeadBit()
        {
            // Dead player (HP=0) gets HP=MaxHp restored + dead bit cleared.
            long masterBase = 0x14184F8C0L;
            var writes = KillEnemiesPlanner.PlanReviveAllies(new[]
            {
                new KillEnemySlot
                {
                    SlotBase = masterBase,
                    Hp = 0, MaxHp = 719, IsPlayer = true,
                }
            });
            Assert.Equal(2, writes.Count);
            var hp = writes.Single(w => w.Address == masterBase + 0x00);
            // 719 = 0x02CF → LE: CF 02
            Assert.Equal(new byte[] { 0xCF, 0x02 }, hp.Bytes);
            var deadClear = writes.Single(w => w.Address == masterBase + 0x31);
            // Clear the dead-bit (0x20) — but preserve other bits in the byte.
            // Simple approach: write 0x00. More accurate: read current byte,
            // mask 0x20 off, write back. For revive, 0x00 is safe since other
            // bits in the +0x31 byte are used only when the unit is dead
            // (observed in session 49 — alive units have +0x31 = 0x00 or 0x10).
            Assert.Equal(new byte[] { 0x00 }, deadClear.Bytes);
        }

        [Fact]
        public void PlanReviveAllies_EnemySlot_SkippedEvenIfDead()
        {
            // Enemy dead doesn't get revived — we're only reviving allies.
            var writes = KillEnemiesPlanner.PlanReviveAllies(new[]
            {
                new KillEnemySlot
                {
                    SlotBase = 0x14184D8C0L,
                    Hp = 0, MaxHp = 647, IsPlayer = false,
                }
            });
            Assert.Empty(writes);
        }

        [Fact]
        public void PlanReviveAllies_MultiplePlayers_TwoWritesEach()
        {
            var slots = new[]
            {
                new KillEnemySlot { SlotBase = 0x14184F8C0L, Hp = 0, MaxHp = 719, IsPlayer = true },  // dead Ramza
                new KillEnemySlot { SlotBase = 0x14184FAC0L, Hp = 0, MaxHp = 437, IsPlayer = true },  // dead Kenrick
                new KillEnemySlot { SlotBase = 0x14184FCC0L, Hp = 628, MaxHp = 628, IsPlayer = true }, // alive Lloyd
            };
            var writes = KillEnemiesPlanner.PlanReviveAllies(slots);
            // 2 players × 2 writes each (HP + dead-bit-clear) = 4 writes.
            Assert.Equal(4, writes.Count);
        }
    }
}
