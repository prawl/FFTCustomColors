using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// A single byte-write operation: "write these bytes at this address."
    /// Pure data — no memory side effects. The dispatcher (CommandWatcher)
    /// turns these into Explorer.WriteByte calls after the plan is built.
    /// </summary>
    public class MemoryWrite
    {
        public long Address { get; set; }
        public byte[] Bytes { get; set; } = System.Array.Empty<byte>();
    }

    /// <summary>
    /// Pure planners for the `cheat_mode_buff` bridge action. Session 47
    /// part 5 — TODO §0 "Claude cheat mode" dev tool for state-collection
    /// playthroughs where fresh-game stats make battles too slow.
    ///
    /// Design: separate PLANNING (which bytes where, pure, tested) from
    /// DISPATCH (calling Explorer.WriteByte, not tested). The planner
    /// returns a list of <see cref="MemoryWrite"/> ops the caller executes.
    ///
    /// Deliberately excludes: level, exp, brave, faith, position. Enemy
    /// scaling depends on party levels, so leveling Ramza up defeats the
    /// purpose. Brave/faith are permanent stat changes that persist to
    /// the roster after battle. Position belongs to placement logic.
    ///
    /// Battle static array offsets (source: NavigationActions.cs:3807-3821):
    ///   +0x0C  exp (NOT WRITTEN)
    ///   +0x0D  level (NOT WRITTEN)
    ///   +0x0E  origBrave (NOT WRITTEN)
    ///   +0x10  origFaith (NOT WRITTEN)
    ///   +0x14  HP (u16)          ← write
    ///   +0x16  MaxHP (u16)       ← write
    ///   +0x22  PA total (u8)     ← write
    ///   +0x5A  element Absorb (u8)    ← write 0xFF (all)
    ///   +0x5B  element Cancel (u8)    ← write 0x00
    ///   +0x5C  element Half (u8)      ← write 0x00
    ///   +0x5D  element Weak (u8)      ← write 0x00 (nothing takes extra dmg)
    ///   +0x5E  element Strengthen (u8) ← write 0x00
    /// </summary>
    public static class BuffPlanner
    {
        private const int OffsetHP = 0x14;
        private const int OffsetMaxHP = 0x16;
        private const int OffsetPA = 0x22;
        private const int OffsetAbsorb = 0x5A;
        private const int OffsetCancel = 0x5B;
        private const int OffsetHalf = 0x5C;
        private const int OffsetWeak = 0x5D;
        private const int OffsetStrengthen = 0x5E;

        /// <summary>
        /// Plan the writes needed to make the unit at <paramref name="slotBase"/>
        /// effectively invincible for one battle. Returns a list of byte-write
        /// operations the dispatcher performs in any order.
        /// </summary>
        /// <param name="slotBase">Absolute address of the unit's slot in the battle static array (at 0x140893C00 + slotIndex*0x200).</param>
        /// <param name="hp">HP / MaxHP value (default 999).</param>
        public static List<MemoryWrite> PlanInvincibilityWrites(long slotBase, int hp = 999)
        {
            var writes = new List<MemoryWrite>
            {
                new()
                {
                    Address = slotBase + OffsetHP,
                    Bytes = new byte[] { (byte)(hp & 0xFF), (byte)((hp >> 8) & 0xFF) },
                },
                new()
                {
                    Address = slotBase + OffsetMaxHP,
                    Bytes = new byte[] { (byte)(hp & 0xFF), (byte)((hp >> 8) & 0xFF) },
                },
                new()
                {
                    Address = slotBase + OffsetPA,
                    Bytes = new byte[] { 0xFF },
                },
                // Absorb = 0xFF (all 8 elements heal). See
                // ElementAffinityDecoder for the bit layout.
                new()
                {
                    Address = slotBase + OffsetAbsorb,
                    Bytes = new byte[] { 0xFF },
                },
                // Zero the others so nothing amplifies. Absorb wins anyway
                // but a stale Weak bit from a prior unit in this slot would
                // otherwise linger.
                new() { Address = slotBase + OffsetCancel, Bytes = new byte[] { 0x00 } },
                new() { Address = slotBase + OffsetHalf, Bytes = new byte[] { 0x00 } },
                new() { Address = slotBase + OffsetWeak, Bytes = new byte[] { 0x00 } },
                new() { Address = slotBase + OffsetStrengthen, Bytes = new byte[] { 0x00 } },
            };
            return writes;
        }
    }
}
