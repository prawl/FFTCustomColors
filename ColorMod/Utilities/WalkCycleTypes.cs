using System;
using System.Collections.Generic;

namespace FFTColorCustomizer.Utilities
{
    /// <summary>
    /// Maps each roster character to its animation rig, so the compositor picks the right arm
    /// offset table. The key is the Images/ folder name, the same key
    /// <see cref="FrameLayout.For"/> uses. See <see cref="WalkCycleType"/> for what the two rigs
    /// are and <see cref="WalkCycle"/> for the tables themselves.
    ///
    /// Classification provenance (do not reclassify from outfit appearance, since
    /// the rig is per-sprite DATA, not a look):
    /// 1. The game's own classic BATTLE.BIN sprite-attributes table (fftpack/battle_bin.en.bin
    ///    inside data/classic/0002.en.pac, table at offset 0x2D748, 159 entries of
    ///    [SHP, SEQ, flying, height], read with the FFTPatcher/Shishi format, 0 = TYPE1,
    ///    1 = TYPE2). Every classic-roster character below matches that table.
    /// 2. A socket-position pixel classifier run over all 66 shipped humanoid HD sheets (the
    ///    arm tile's content centroid plus a candidate rig's offset must land at the anatomical
    ///    socket position measured from TYPE1 exemplar sheets; body poses are shared between
    ///    rigs, so the socket is rig-independent). Its verdict agreed with the BATTLE.BIN table
    ///    on every character that has a classic entry, and settled the ones that don't:
    ///    DarkKnight/OnionKnight (PSP-era additions, TYPE1) and Lettie (IVC-only NPC whose
    ///    sheet's alpha mask is pixel-identical to Celia's, TYPE2).
    /// 3. The owner's live pass. Note, Bard and Calculator LOOK fine under TYPE1 in motion,
    ///    but both the game table and zoomed per-frame renders (back-cycle f18 detaches the
    ///    fist from the cuff) prove they are TYPE2 rigs; the data verdict stands.
    ///
    /// Monsters are classified from the same BATTLE.BIN table
    /// (entries 0x86-0x9A): Goblin (0x87) and Skeleton (0x8B) are TYPE1 rigs and
    /// Mindflayer/Squid (0x8A) is TYPE2 (their sheets really are humanoid layouts), Chocobo
    /// (0x86) is CYOKO, and every other family plus Construct 8 / Steel Giant (0x9A) is MON.
    /// The MON and CYOKO tables live in <see cref="MonsterWalkCycle"/>.
    /// </summary>
    public static class WalkCycleTypes
    {
        private static readonly HashSet<string> Type2Characters = new HashSet<string>(StringComparer.Ordinal)
        {
            // Generic jobs on the robed/skirted rig (classic table 0x62-0x63, 0x6A-0x6D,
            // 0x6E-0x71, 0x74-0x79, 0x80-0x85).
            "Chemist_Male", "Chemist_Female",
            "WhiteMage_Male", "WhiteMage_Female",
            "BlackMage_Male", "BlackMage_Female",
            "TimeMage_Male", "TimeMage_Female",
            "Summoner_Male", "Summoner_Female",
            "Mystic_Male", "Mystic_Female",
            "Mediator_Male", "Mediator_Female",
            "Geomancer_Male", "Geomancer_Female",
            "Calculator_Male", "Calculator_Female",
            "Bard_Male", "Dancer_Female",
            "Mime_Male", "Mime_Female",

            // Story characters and NPCs on the TYPE2 rig (classic table 0x0C Ovelia, 0x0F Reis,
            // 0x10 Zalmo, 0x13 Simon, 0x14 Alma, 0x1B Elmdor, 0x21 Balmafula, 0x29 Rafa,
            // 0x2D Celia; Lettie by pixel identity with Celia's sheet).
            "Alma", "Celia", "Elmdore", "Lettie", "Ovelia",
            "Rapha", "Reis", "Simon", "Valmafra", "Zalmour",

            // Mindflayer ("Squid", classic table 0x8A) is the one TYPE2-rig monster, its
            // sheet is a genuine humanoid layout, like Goblin's and Skeleton's are TYPE1.
            "Mindflayer"
        };

        // Monster rigs from the same BATTLE.BIN table. Goblin and Skeleton are
        // ABSENT on purpose, they are TYPE1 (the default), like any unlisted character.
        private static readonly Dictionary<string, WalkCycleType> MonsterRigs = new Dictionary<string, WalkCycleType>(StringComparer.Ordinal)
        {
            ["Chocobo"] = WalkCycleType.Cyoko,     // 0x86 CYOKO
            ["Bomb"] = WalkCycleType.Mon,          // 0x88
            ["Panther"] = WalkCycleType.Mon,       // 0x89 Coeurl
            ["Ghost"] = WalkCycleType.Mon,         // 0x8C
            ["Ahriman"] = WalkCycleType.Mon,       // 0x8D
            ["Aevis"] = WalkCycleType.Mon,         // 0x8E Cockatrice
            ["Pig"] = WalkCycleType.Mon,           // 0x8F Uribo
            ["Treant"] = WalkCycleType.Mon,        // 0x90
            ["Minotaur"] = WalkCycleType.Mon,      // 0x91
            ["Malboro"] = WalkCycleType.Mon,       // 0x92
            ["Behemoth"] = WalkCycleType.Mon,      // 0x93
            ["Dragon"] = WalkCycleType.Mon,        // 0x94
            ["Hydra"] = WalkCycleType.Mon,         // 0x95 Tiamat
            ["Construct8"] = WalkCycleType.Mon     // 0x9A Steel Giant
        };

        /// <summary>
        /// The rig for <paramref name="characterName"/> (an Images/ folder name). Unknown names
        /// default to <see cref="WalkCycleType.Type1"/>: it is the majority rig and matches the
        /// pre-round-3 behavior, so a future roster addition degrades to the old (visible,
        /// reportable) arm placement instead of throwing.
        /// </summary>
        public static WalkCycleType For(string characterName)
        {
            if (MonsterRigs.TryGetValue(characterName, out var monsterRig))
                return monsterRig;
            return Type2Characters.Contains(characterName) ? WalkCycleType.Type2 : WalkCycleType.Type1;
        }

        /// <summary>
        /// Whether a character's sheet can play a walk cycle at all. Monster rigs
        /// always can (their tables address the sheet directly); humanoid rigs need either
        /// the standard layout the humanoid tables assume, or an explicit-rect monster sheet
        /// (Goblin, Skeleton, Mindflayer), whose content IS the standard humanoid layout even
        /// though the static poses are picked by hand-drawn boxes.
        /// </summary>
        public static bool SheetSupportsWalk(FrameLayout layout, WalkCycleType rig)
        {
            if (rig == WalkCycleType.Mon || rig == WalkCycleType.Cyoko)
                return true;
            return layout.SupportsWalkCycle || layout.UsesExplicitRects;
        }
    }
}
