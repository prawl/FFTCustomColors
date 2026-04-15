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
            public int CurrentJobJp; // +0x80 u16 — JP in the unit's currently-equipped class
            public int NameId;      // +0x230 (u16)
            /// <summary>
            /// Zero-indexed position in the PartyMenu Units grid when the Sort
            /// option is set to "Time Recruited" (the game's default). Stored
            /// by the engine at roster +0x122 (1 byte). Discovered 2026-04-14
            /// by dumping all 14 active slots and finding a perfect monotonic
            /// ranking at this offset in display order. Unlike `UnitIndex`
            /// this is NOT the slot number — Mustadio (slot 11) has
            /// DisplayOrder=4 because he's the 5th unit in the grid.
            /// </summary>
            public int DisplayOrder; // +0x122
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
        /// Reads the 7 equipment u16 fields at +0x0E..+0x1A for a single
        /// roster slot. Returns null if the slot is empty or the read fails.
        /// </summary>
        public EquipmentReader.Loadout? ReadLoadout(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots) return null;
            long b = RosterBase + (long)slotIndex * SlotStride;
            var reads = new (System.IntPtr addr, int size)[7];
            for (int i = 0; i < 7; i++)
                reads[i] = ((System.IntPtr)(b + 0x0E + i * 2), 2);
            var v = _explorer.ReadMultiple(reads);
            var u16s = new int[7];
            for (int i = 0; i < 7; i++) u16s[i] = (int)v[i];
            return EquipmentReader.FromSlotValues(u16s);
        }

        /// <summary>
        /// Equipped non-equipment ability slots for a unit:
        ///   - Secondary skillset name (e.g. "Items", "White Magicks") — from +0x07
        ///   - Reaction / Support / Movement ability names — from +0x08..+0x0D
        /// Each passive ability has an ID byte + an "equipped" flag byte. We only
        /// surface the name when the flag byte is 1; otherwise the slot is empty.
        /// Also reports the **primary** skillset (job-locked, derived from the
        /// unit's currently-equipped class via CommandWatcher.GetPrimarySkillsetByJobName).
        /// </summary>
        public class AbilityLoadout
        {
            public string? Primary;       // job-locked skillset (e.g. "Mettle" for Gallant Knight)
            public string? Secondary;     // selectable skillset
            public string? Reaction;
            public string? Support;
            public string? Movement;
        }

        /// <summary>
        /// Per-job list of passive ability IDs sorted ascending. The roster's
        /// per-job learned bitfield at +0x32 + jobIdx*3 uses BYTE 2 of that
        /// triplet to track which of these passives the unit has purchased,
        /// MSB-first: bit 0x80 → index 0 (lowest ID), 0x40 → index 1, etc.
        ///
        /// Verified 2026-04-14 live on Ramza: 19 set bits across all jobs'
        /// byte 2 values exactly matched the 19 reactions shown in the game's
        /// Reaction Abilities picker. Same decoding works for supports and
        /// movements (byte 2 mixes all three types; we classify per ability ID
        /// via AbilityData dicts).
        /// </summary>
        private static readonly (int jobIdx, byte[] ids)[] _passivesByJob = new[]
        {
            // Squire = jobIdx 0
            (0, new byte[] { 0xB4, 0xCC, 0xCF, 0xDE, 0xDF, 0xE6 }),
            // Chemist = 1
            (1, new byte[] { 0xB9, 0xDA, 0xDB, 0xE0, 0xFD }),
            // Knight = 2
            (2, new byte[] { 0xBF, 0xC6, 0xC7, 0xC8 }),
            // Archer = 3
            (3, new byte[] { 0xA8, 0xC4, 0xCA, 0xD5, 0xE9 }),
            // Monk = 4
            (4, new byte[] { 0xAF, 0xBA, 0xC5, 0xD8, 0xED }),
            // White Mage = 5
            (5, new byte[] { 0xAC, 0xD4 }),
            // Black Mage = 6
            (6, new byte[] { 0xB3, 0xD3 }),
            // Time Mage = 7
            (7, new byte[] { 0xB1, 0xBD, 0xE2, 0xF2, 0xFA }),
            // Summoner = 8
            (8, new byte[] { 0xB0, 0xCE }),
            // Thief = 9
            (9, new byte[] { 0xAA, 0xB7, 0xC2, 0xD7, 0xE7, 0xEA }),
            // Orator = 10
            (10, new byte[] { 0xC0, 0xCD, 0xD6, 0xD9 }),
            // Mystic = 11
            (11, new byte[] { 0xB6, 0xD2, 0xEE, 0xF4 }),
            // Geomancer = 12
            (12, new byte[] { 0xB5, 0xD1, 0xF5, 0xF8 }),
            // Dragoon = 13
            (13, new byte[] { 0xAB, 0xCB, 0xEC }),
            // Samurai = 14
            (14, new byte[] { 0xB2, 0xC3, 0xC9, 0xDC, 0xF7 }),
            // Ninja = 15
            (15, new byte[] { 0xA9, 0xC1, 0xDD, 0xF6 }),
            // Arithmetician = 16
            (16, new byte[] { 0xBC, 0xBE, 0xD0, 0xEF, 0xF0 }),
            // Bard/Dance = 17 (shared slot)
            (17, new byte[] { 0xA7, 0xAE, 0xE8, 0xFB }),
            // Dark Knight = 19
            (19, new byte[] { 0xE4, 0xE5, 0xEB }),
        };

        /// <summary>
        /// Reads every passive ability ID the unit has learned, across every job.
        /// Decodes byte 2 of the per-job bitfield at +0x32 + jobIdx*3 + 2.
        /// Result includes reaction, support, and movement abilities mixed;
        /// caller classifies via AbilityData.ReactionAbilities / SupportAbilities
        /// / MovementAbilities.
        /// </summary>
        public List<byte> ReadLearnedPassives(int slotIndex)
        {
            var result = new List<byte>();
            if (slotIndex < 0 || slotIndex >= MaxSlots) return result;

            long b = RosterBase + (long)slotIndex * SlotStride;
            var reads = new (System.IntPtr addr, int size)[_passivesByJob.Length];
            for (int j = 0; j < _passivesByJob.Length; j++)
                reads[j] = ((System.IntPtr)(b + 0x32 + _passivesByJob[j].jobIdx * 3 + 2), 1);
            var v = _explorer.ReadMultiple(reads);

            for (int j = 0; j < _passivesByJob.Length; j++)
            {
                byte byte2 = (byte)(int)v[j];
                var ids = _passivesByJob[j].ids;
                for (int i = 0; i < ids.Length; i++)
                {
                    if ((byte2 & (0x80 >> i)) != 0)
                        result.Add(ids[i]);
                }
            }
            return result;
        }

        /// <summary>
        /// Reads which secondary skillsets the unit has unlocked. A skillset is
        /// considered unlocked iff at least one of its action-ability bits is
        /// set in the per-job learned bitfield at +0x32 + jobIdx*3 (bytes 0-1,
        /// MSB-first per `project_roster_learned_abilities.md`).
        ///
        /// This is a proxy — strictly the game considers a skillset selectable
        /// based on job-unlock state (achieved by spending JP), but learned
        /// abilities are a strong correlate (you can only learn abilities for
        /// jobs you've unlocked). Returns canonical skillset names from
        /// AbilityData.GetJobIdxBySkillsetName.
        ///
        /// Iterates ALL 20 job indices (0-19, with 17 shared by Bard/Dance).
        /// </summary>
        public List<string> ReadUnlockedSkillsets(int slotIndex)
        {
            var result = new List<string>();
            if (slotIndex < 0 || slotIndex >= MaxSlots) return result;

            long b = RosterBase + (long)slotIndex * SlotStride;
            // Read all 20 jobs' bitfields in one round trip (3 bytes each, but
            // only bytes 0-1 carry action-ability bits).
            const int Jobs = 20;
            var reads = new (System.IntPtr addr, int size)[Jobs * 2];
            for (int j = 0; j < Jobs; j++)
            {
                reads[j * 2 + 0] = ((System.IntPtr)(b + 0x32 + j * 3 + 0), 1);
                reads[j * 2 + 1] = ((System.IntPtr)(b + 0x32 + j * 3 + 1), 1);
            }
            var v = _explorer.ReadMultiple(reads);

            // Map jobIdx → canonical skillset name. We use the inverse of
            // AbilityData.GetJobIdxBySkillsetName. Mettle and Fundaments share
            // jobIdx 0; we surface "Mettle" as the canonical name there since
            // the game UI labels Squire's primary as Mettle in this remaster.
            string?[] skillsets = new string?[Jobs]
            {
                "Mettle",         // 0 (also Fundaments)
                "Items",          // 1
                "Arts of War",    // 2
                "Aim",            // 3
                "Martial Arts",   // 4
                "White Magicks",  // 5
                "Black Magicks",  // 6
                "Time Magicks",   // 7
                "Summon",         // 8
                "Steal",          // 9
                "Speechcraft",    // 10
                "Mystic Arts",    // 11
                "Geomancy",       // 12
                "Jump",           // 13
                "Iaido",          // 14
                "Throw",          // 15
                "Arithmeticks",   // 16
                "Bardsong",       // 17 (also Dance)
                null,             // 18 reserved
                "Darkness",       // 19
            };
            for (int j = 0; j < Jobs; j++)
            {
                if (skillsets[j] == null) continue;
                int byte0 = (int)v[j * 2 + 0];
                int byte1 = (int)v[j * 2 + 1];
                if (byte0 != 0 || byte1 != 0)
                    result.Add(skillsets[j]!);
            }
            return result;
        }

        /// <summary>
        /// Reads equipped ability slots for the given roster slot. Returns null
        /// if the slot is out of range. Slot fields that are empty (equipped
        /// flag == 0) come back as null on the AbilityLoadout. Primary is
        /// resolved from the unit's job name via CommandWatcher.
        /// </summary>
        public AbilityLoadout? ReadEquippedAbilities(int slotIndex, string? jobName)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots) return null;
            long b = RosterBase + (long)slotIndex * SlotStride;
            var reads = new (System.IntPtr addr, int size)[]
            {
                ((System.IntPtr)(b + 0x07), 1), // secondary skillset id
                ((System.IntPtr)(b + 0x08), 1), // reaction id
                ((System.IntPtr)(b + 0x09), 1), // reaction equipped flag
                ((System.IntPtr)(b + 0x0A), 1), // support id
                ((System.IntPtr)(b + 0x0B), 1), // support equipped flag
                ((System.IntPtr)(b + 0x0C), 1), // movement id
                ((System.IntPtr)(b + 0x0D), 1), // movement equipped flag
            };
            var v = _explorer.ReadMultiple(reads);

            var result = new AbilityLoadout
            {
                Primary = jobName != null
                    ? Utilities.CommandWatcher.GetPrimarySkillsetByJobName(jobName)
                    : null,
                Secondary = Utilities.CommandWatcher.GetSkillsetName((int)v[0]),
            };
            if ((int)v[2] == 1)
            {
                byte id = (byte)(int)v[1];
                if (AbilityData.ReactionAbilities.TryGetValue(id, out var ra))
                    result.Reaction = ra.Name;
            }
            if ((int)v[4] == 1)
            {
                byte id = (byte)(int)v[3];
                if (AbilityData.SupportAbilities.TryGetValue(id, out var sa))
                    result.Support = sa.Name;
            }
            if ((int)v[6] == 1)
            {
                byte id = (byte)(int)v[5];
                if (AbilityData.MovementAbilities.TryGetValue(id, out var ma))
                    result.Movement = ma.Name;
            }
            return result;
        }

        /// <summary>
        /// Reads all non-empty roster slots. Returns them in slot order (Ramza
        /// first). Empty slots (level == 0) are filtered out.
        /// </summary>
        public List<RosterSlot> ReadAll()
        {
            var result = new List<RosterSlot>();

            // Batch-read the small set of fields we need for every slot in one round-trip.
            const int FieldsPerSlot = 11;
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
                reads[s * FieldsPerSlot + 9] = ((System.IntPtr)(b + 0x80), 2); // current-job JP u16
                reads[s * FieldsPerSlot + 10] = ((System.IntPtr)(b + 0x122), 1); // display order (Time Recruited)
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
                    CurrentJobJp = (int)v[s * FieldsPerSlot + 9],
                    NameId = f.NameId,
                    DisplayOrder = (int)v[s * FieldsPerSlot + 10],
                    JobName = ResolveJobName(f.NameId, f.Job),
                };

                // Story characters have hardcoded names keyed by nameId.
                // Generic recruits need the per-slot name table lookup.
                slot.Name = UnitNameLookup.GetName(f.NameId) ?? _nameTable.GetNameBySlot(s);

                result.Add(slot);
            }

            return result;
        }

        /// <summary>
        /// Returns the roster slot at the given position in the PartyMenu grid
        /// (left-to-right, top-to-bottom in display order). Display order is
        /// driven by roster byte +0x122, which the game writes when sorting
        /// under the current Sort option (default: Time Recruited). Returns
        /// null if no slot occupies that display position (e.g. 14 units but
        /// displayIndex=14).
        /// </summary>
        public RosterSlot? GetSlotByDisplayOrder(int displayIndex)
        {
            foreach (var slot in ReadAll())
            {
                if (slot.DisplayOrder == displayIndex) return slot;
            }
            return null;
        }
    }
}
