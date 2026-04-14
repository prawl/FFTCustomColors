using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Reads per-slot roster data from the static array at 0x1411A18D0.
    /// Stride is 0x258 bytes, 50 slots max. Empty slots have Level == 0
    /// (verified 2026-04-14 that unitIndex == 0xFF does NOT mean empty —
    /// several story characters in active rosters carry unitIndex 0xFF).
    ///
    /// This is the roster as persisted to save state — stable outside battle.
    /// For live-battle data (positions, current HP, statuses) use the battle
    /// array scan in NavigationActions.CollectUnitPositionsFull.
    /// </summary>
    public class RosterReader
    {
        public const long RosterBase = 0x1411A18D0;
        public const int SlotStride = 0x258;
        public const int MaxSlots = 50;

        private readonly MemoryExplorer _explorer;
        private readonly NameTableLookup _nameTable;

        public RosterReader(MemoryExplorer explorer, NameTableLookup nameTable)
        {
            _explorer = explorer;
            _nameTable = nameTable;
        }

        public class RosterSlot
        {
            public int SlotIndex;
            public int SpriteSet;   // +0x00
            public int UnitIndex;   // +0x01 (NOT a reliable emptiness marker)
            public int Job;         // +0x02
            public int Exp;         // +0x1C
            public int Level;       // +0x1D
            public int Brave;       // +0x1E
            public int Faith;       // +0x1F
            public int NameId;      // +0x230 (u16)
            public string? Name;    // Resolved via UnitNameLookup → NameTableLookup
            public string? JobName; // Resolved via CharacterData.GetJobName
        }

        /// <summary>
        /// Raw per-slot byte fields for the pure parser (no memory dependency).
        /// </summary>
        public struct RawSlotFields
        {
            public int SpriteSet;
            public int UnitIndex;
            public int Job;
            public int Exp;
            public int Level;
            public int Brave;
            public int Faith;
            public int NameId;
        }

        /// <summary>
        /// Pure slot filter: a slot is in the active party roster iff
        /// unitIndex != 0xFF AND level > 0. Verified 2026-04-14 live: the
        /// party menu reports 16/50 units and iterating 0..49 keeping only
        /// unitIndex != 0xFF gives exactly the displayed 16. Story characters
        /// currently in the party (Orlandeau, Reis, Mustadio, etc.) have a
        /// real unitIndex assigned — unitIndex = 0xFF indicates a monster or
        /// generic TEMPLATE that the engine keeps behind the roster (Treant,
        /// duplicate Construct 8, spare Mustadio/Agrias clones) and that
        /// should NOT be surfaced as a grid tile.
        /// </summary>
        public static bool IsEmptySlot(RawSlotFields f)
        {
            if (f.Level == 0) return true;
            return f.UnitIndex == 0xFF;
        }

        /// <summary>
        /// Pure job-name resolution.
        ///
        /// The tricky part is that the IC remaster roster job byte +0x02
        /// uses two different numbering spaces depending on unit type:
        ///   - Generics: roster job IDs 74-95 (0x4A-0x5F) — Knight, Dragoon,
        ///     Monk, ... — plus Ramza's Gallant Knight (3).
        ///   - Story characters locked to their canonical class: the byte
        ///     often equals their nameId (Mustadio 22, Agrias 30, Rapha 41,
        ///     etc.), which collides with the PSX small-number dict and
        ///     would resolve to the wrong job if we use GetJobName blindly.
        ///
        /// So we first decide whether this is a known story character
        /// (nameId in StoryCharacterJob). If yes AND the +0x02 byte equals
        /// their nameId, they're on their canonical class — return that.
        /// Otherwise the story character has changed jobs, in which case
        /// the byte is a real roster job ID and GetJobName resolves it.
        /// Generic units always go through GetJobName (roster dict first,
        /// PSX fallback second).
        /// </summary>
        public static string? ResolveJobName(int nameId, int job)
        {
            var storyJob = CharacterData.GetStoryJob(nameId);
            if (storyJob != null && job == nameId)
                return storyJob; // canonical class, unchanged
            if (job > 0)
                return CharacterData.GetJobName(job);
            return storyJob;
        }

        /// <summary>
        /// Reads all non-empty roster slots. Returns them in slot order (Ramza
        /// first). Empty slots (level == 0) are filtered out.
        /// </summary>
        public List<RosterSlot> ReadAll()
        {
            var result = new List<RosterSlot>();

            // Batch-read the small set of fields we need for every slot in one round-trip.
            const int FieldsPerSlot = 9;
            var reads = new (System.IntPtr addr, int size)[MaxSlots * FieldsPerSlot];
            for (int s = 0; s < MaxSlots; s++)
            {
                long b = RosterBase + (long)s * SlotStride;
                reads[s * FieldsPerSlot + 0] = ((System.IntPtr)(b + 0x00), 1); // spriteSet
                reads[s * FieldsPerSlot + 1] = ((System.IntPtr)(b + 0x01), 1); // unitIndex
                reads[s * FieldsPerSlot + 2] = ((System.IntPtr)(b + 0x02), 1); // job
                reads[s * FieldsPerSlot + 3] = ((System.IntPtr)(b + 0x1C), 1); // exp
                reads[s * FieldsPerSlot + 4] = ((System.IntPtr)(b + 0x1D), 1); // level
                reads[s * FieldsPerSlot + 5] = ((System.IntPtr)(b + 0x1E), 1); // brave
                reads[s * FieldsPerSlot + 6] = ((System.IntPtr)(b + 0x1F), 1); // faith
                reads[s * FieldsPerSlot + 7] = ((System.IntPtr)(b + 0x230), 2); // nameId u16
                reads[s * FieldsPerSlot + 8] = ((System.IntPtr)(b + 0x20), 1); // reserved probe
            }
            var v = _explorer.ReadMultiple(reads);

            for (int s = 0; s < MaxSlots; s++)
            {
                var f = new RawSlotFields
                {
                    SpriteSet = (int)v[s * FieldsPerSlot + 0],
                    UnitIndex = (int)v[s * FieldsPerSlot + 1],
                    Job       = (int)v[s * FieldsPerSlot + 2],
                    Exp       = (int)v[s * FieldsPerSlot + 3],
                    Level     = (int)v[s * FieldsPerSlot + 4],
                    Brave     = (int)v[s * FieldsPerSlot + 5],
                    Faith     = (int)v[s * FieldsPerSlot + 6],
                    NameId    = (int)v[s * FieldsPerSlot + 7],
                };

                if (IsEmptySlot(f)) continue;

                var slot = new RosterSlot
                {
                    SlotIndex = s,
                    SpriteSet = f.SpriteSet,
                    UnitIndex = f.UnitIndex,
                    Job = f.Job,
                    Exp = f.Exp,
                    Level = f.Level,
                    Brave = f.Brave,
                    Faith = f.Faith,
                    NameId = f.NameId,
                    JobName = ResolveJobName(f.NameId, f.Job),
                };

                // Story characters have hardcoded names keyed by nameId.
                // Generic recruits need the per-slot name table lookup.
                slot.Name = UnitNameLookup.GetName(f.NameId) ?? _nameTable.GetNameBySlot(s);

                result.Add(slot);
            }

            return result;
        }
    }
}
