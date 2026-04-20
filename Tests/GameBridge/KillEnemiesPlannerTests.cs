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
    }
}
