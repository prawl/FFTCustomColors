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

        /// <summary>
        /// Optional: the battle-array slot address for this unit (at
        /// 0x140893C00 + slotIndex*0x200). When set, the planner can
        /// emit Reraise-clear writes targeting the status byte at
        /// battle-array slot +0x47 (5-byte status bitfield byte 2,
        /// bit 0x20). Leave at 0 if the caller doesn't have the
        /// battle-array slot; Reraise-clear is skipped.
        /// </summary>
        public long BattleArraySlotBase { get; set; }

        /// <summary>
        /// Current value of the status bitfield byte at battle-array
        /// slot +0x47 (byte 2 of the 5-byte field at +0x45). The
        /// planner reads this to know whether Reraise (bit 0x20) is
        /// set and emits a clear-write with the other bits preserved.
        /// </summary>
        public byte CurrentStatusByte2 { get; set; }
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

        /// <summary>Battle-array offset for the 5-byte status bitfield (+0x45).
        /// Byte 2 of that field (+0x47) holds the Reraise bit (0x20). Session
        /// 49 Lloyd vs Ramza diff: Lloyd (Reraise+Regen) had +0x47=0x20, Ramza
        /// (Regen only) had +0x47=0x00. StatusDecoder.cs maps (2, 0x20, "Reraise").</summary>
        public const int OffsetStatusByte2 = 0x47;
        public const byte ReraiseBitMask = 0x20;

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

                // Optional Reraise-clear: only emit when the caller supplied
                // both the battle-array slot AND the current status byte 2
                // value actually has the Reraise bit set. Keeps the write
                // count minimal for the non-undead case (2 writes/slot).
                if (slot.BattleArraySlotBase != 0
                    && (slot.CurrentStatusByte2 & ReraiseBitMask) != 0)
                {
                    byte cleared = (byte)(slot.CurrentStatusByte2 & ~ReraiseBitMask);
                    writes.Add(new MemoryWrite
                    {
                        Address = slot.BattleArraySlotBase + OffsetStatusByte2,
                        Bytes = new byte[] { cleared }
                    });
                }
            }
            return writes;
        }

        /// <summary>
        /// Session 49 — dev tool. Plan writes to revive every dead (HP=0)
        /// player-team slot to full HP and clear the dead-bit. Enemies and
        /// already-live players are skipped.
        /// </summary>
        public static List<MemoryWrite> PlanReviveAllies(IEnumerable<KillEnemySlot> slots)
        {
            var writes = new List<MemoryWrite>();
            if (slots == null) return writes;

            foreach (var slot in slots)
            {
                if (slot == null) continue;
                if (!slot.IsPlayer) continue;
                if (slot.MaxHp <= 0) continue;
                if (slot.Hp > 0) continue; // already alive

                byte lo = (byte)(slot.MaxHp & 0xFF);
                byte hi = (byte)((slot.MaxHp >> 8) & 0xFF);
                writes.Add(new MemoryWrite
                {
                    Address = slot.SlotBase + OffsetHp,
                    Bytes = new byte[] { lo, hi }
                });
                writes.Add(new MemoryWrite
                {
                    Address = slot.SlotBase + OffsetDeadFlag,
                    Bytes = new byte[] { 0x00 }
                });
            }
            return writes;
        }
    }
}
