using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// One slot in the master HP table the planner sees. Pure data — the
    /// dispatcher snapshots real memory into these records before calling.
    /// Session 49: master HP table discovered at runtime ~0x14184xxxx, stride
    /// 0x200. See memory/project_master_hp_store.md.
    /// </summary>
    public class KillEnemySlot
    {
        public long SlotBase { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public bool IsPlayer { get; set; }
    }

    /// <summary>
    /// Pure planner for the `cheat_kill_enemies` bridge action. Given a
    /// snapshot of the master HP table, returns the byte-writes needed to
    /// KO every non-player slot with HP &gt; 0. Session 49 shipping pattern:
    /// planner (tested) + dispatcher (untested, does memory reads/writes).
    ///
    /// Per-slot kill recipe (verified live session 49 Siedge Weald):
    ///   +0x00  u16 HP    = 0
    ///   +0x31  u8  dead-bit (bit 5, mask 0x20) set
    /// The game's victory check on next turn rollover transitions to
    /// BattleVictory when every enemy slot has HP=0 + dead bit set.
    ///
    /// Undead caveat: Reraise may re-animate a dead-flagged undead on the
    /// same turn. Not handled here — callers can layer status clears later.
    /// </summary>
    public static class KillEnemiesPlanner
    {
        public const int OffsetHp = 0x00;
        public const int OffsetDeadFlag = 0x31;
        public const byte DeadBitMask = 0x20;

        /// <summary>
        /// Plan writes to KO every enemy slot with HP &gt; 0. Player slots
        /// and empty slots (MaxHp == 0) are skipped.
        /// </summary>
        public static List<MemoryWrite> Plan(IEnumerable<KillEnemySlot> slots)
        {
            var writes = new List<MemoryWrite>();
            if (slots == null) return writes;

            foreach (var slot in slots)
            {
                if (slot == null) continue;
                if (slot.IsPlayer) continue;
                if (slot.MaxHp <= 0) continue;
                if (slot.Hp <= 0) continue;

                writes.Add(new MemoryWrite
                {
                    Address = slot.SlotBase + OffsetHp,
                    Bytes = new byte[] { 0x00, 0x00 }
                });
                writes.Add(new MemoryWrite
                {
                    Address = slot.SlotBase + OffsetDeadFlag,
                    Bytes = new byte[] { DeadBitMask }
                });
            }
            return writes;
        }
    }
}
