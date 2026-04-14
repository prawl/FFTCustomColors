using System.Collections.Generic;
using System.Linq;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Reads the live "available abilities" list from the picker widget that
    /// opens when the player presses Enter on Reaction/Support/Movement (and
    /// possibly Secondary) on EquipmentAndAbilities.
    ///
    /// Layout discovered live 2026-04-14 on Ramza's Reaction picker:
    ///   The picker widget allocates 1+ structs with shape:
    ///     +0x00..+0x07  vtable pointer
    ///     +0x08..+0x0B  u32 count
    ///     +0x0C..+0x0F  padding
    ///     +0x10..+0x10+count  u8 ability IDs in display order
    ///     +0x10+count..      padding
    ///     trailing UE4 widget metadata
    ///
    ///   The MASTER list (count = total learned abilities of that type) is the
    ///   one we want; sibling structs are visible-page subsets. We pick the
    ///   highest-count match whose IDs all decode to a valid ability of the
    ///   target type (reaction / support / movement).
    ///
    ///   Order in the master list matches the in-game picker — including the
    ///   currently-equipped ability appearing first. So we don't have to
    ///   re-sort; just decode and return.
    ///
    /// Strategy: AoB-search for the count-byte sequence `LL 00 00 00 00 00 00 00`
    /// where LL is a small u8 (1..32 covers any plausible learned count) at
    /// position 0x08, followed by 0x10 with a candidate ability ID at +0x08
    /// (or just trust the search and verify each match's IDs are all in the
    /// expected dictionary).
    ///
    /// This is volatile heap data — only valid while the picker is open. Caller
    /// should call this once per screen poll.
    /// </summary>
    public class PickerListReader
    {
        private readonly MemoryExplorer _explorer;

        public PickerListReader(MemoryExplorer explorer)
        {
            _explorer = explorer;
        }

        public enum PickerType
        {
            Reaction,
            Support,
            Movement,
        }

        /// <summary>
        /// Find the master ability list for the picker that's currently open.
        /// Returns ordered ability IDs as displayed in-game, or empty list if
        /// the picker can't be located (closed, or signature drift).
        /// </summary>
        public List<byte> ReadMasterList(PickerType type, byte equippedAbilityId)
        {
            // Strategy: search for "<equipped> at +0x10" with u32 count at +0x08.
            // The equipped ability is always first in the picker's display order
            // (verified live), so the master list always starts with it.
            //
            // We can't search for `count <equipped>` because count varies. Instead,
            // search for the equipped byte ID alone is too generic. Use a
            // longer signature: count must be 1..32 (a u8 + 7 zeros for u64),
            // followed at +0x08 (relative to the count) by the equipped byte.
            //
            // Pattern: [count u8] 00 00 00 00 00 00 00 [equipped u8]
            //           ^----offset 0----^ (count)        ^offset 8 (first id)
            //
            // We have to try each plausible count value 1..32.
            //
            // To narrow further: the picker structs we found had 8 zero bytes
            // BEFORE the count too (the high 4 bytes of the 64-bit pointer
            // are typically `7F` or similar but the trailing padding helps).
            // For now, the simpler [count, 7 zeros, id] pattern is a strong
            // enough signature in our experiments — both the master list AND
            // visible-page subsets matched and we just pick the largest count.

            var validIds = type switch
            {
                PickerType.Reaction => AbilityData.ReactionAbilities.Keys.ToHashSet(),
                PickerType.Support => AbilityData.SupportAbilities.Keys.ToHashSet(),
                PickerType.Movement => AbilityData.MovementAbilities.Keys.ToHashSet(),
                _ => new HashSet<byte>(),
            };

            // Try counts 1..32 — practical bound on how many of one passive
            // type a single character can have learned. Stop early once we
            // get a confident master-list match.
            (int count, long addr)? bestMatch = null;

            for (int c = 32; c >= 1; c--) // descending so we find biggest first
            {
                // Build pattern: [c] 00*7 [equippedId]
                var pattern = new byte[9];
                pattern[0] = (byte)c;
                pattern[8] = equippedAbilityId;

                var hexStr = string.Join(" ", pattern.Select(b => b.ToString("X2")));
                var matches = _explorer.SearchBytesInAllMemory(
                    pattern, maxResults: 20);

                foreach (var (matchAddr, _) in matches)
                {
                    // Read the c bytes starting at +0x08 (i.e. starting at
                    // matchAddr + 8 since matchAddr points at the count byte).
                    var listBytes = _explorer.Scanner.ReadBytes(
                        (nint)((long)matchAddr + 8), c);
                    if (listBytes == null || listBytes.Length < c) continue;

                    // Verify ALL c IDs are valid for this picker type. If any
                    // don't decode, this isn't an ability list — false match.
                    bool allValid = true;
                    for (int i = 0; i < c; i++)
                    {
                        if (!validIds.Contains(listBytes[i])) { allValid = false; break; }
                    }
                    if (!allValid) continue;

                    // First valid match at this count — return it. Descending
                    // search means this is the largest valid count.
                    return listBytes.Take(c).ToList();
                }

                // Optimization: if we already found ANY match at a higher
                // count, no need to try smaller. But the loop already
                // descends, so the first valid match is the biggest.
                if (bestMatch.HasValue) break;
            }

            return new List<byte>();
        }
    }
}
