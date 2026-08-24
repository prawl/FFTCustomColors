using System;
using System.Collections.Generic;

namespace FFTColorCustomizer.Utilities
{
    /// <summary>
    /// Maps each roster character (by Images/ folder name, the same key
    /// <see cref="FrameLayout.For"/> uses) to its animation rig, so the walk-cycle compositor
    /// picks the right arm-offset table. See <see cref="WalkCycleType"/> for what the two rigs
    /// are and <see cref="WalkCycle"/> for the tables themselves.
    ///
    /// Classification provenance (CC-36 round 3; do not reclassify from outfit appearance -
    /// the rig is per-sprite DATA, not a look):
    /// 1. The game's own classic BATTLE.BIN sprite-attributes table (fftpack/battle_bin.en.bin
    ///    inside data/classic/0002.en.pac, table at offset 0x2D748, 159 entries of
    ///    [SHP, SEQ, flying, height], read with the FFTPatcher/Shishi format: 0 = TYPE1,
    ///    1 = TYPE2). Every classic-roster character below matches that table.
    /// 2. A socket-position pixel classifier run over all 66 shipped humanoid HD sheets (the
    ///    arm tile's content centroid plus a candidate rig's offset must land at the anatomical
    ///    socket position measured from TYPE1 exemplar sheets; body poses are shared between
    ///    rigs, so the socket is rig-independent). Its verdict agreed with the BATTLE.BIN table
    ///    on every character that has a classic entry, and settled the ones that don't:
    ///    DarkKnight/OnionKnight (PSP-era additions, TYPE1) and Lettie (IVC-only NPC whose
    ///    sheet's alpha mask is pixel-identical to Celia's, TYPE2).
    /// 3. The owner's live pass. Note: Bard and Calculator LOOK fine under TYPE1 in motion,
    ///    but both the game table and zoomed per-frame renders (back-cycle f18 detaches the
    ///    fist from the cuff) prove they are TYPE2 rigs; the data verdict stands.
    ///
    /// Monsters, Chocobo and Construct 8 never reach this registry: their layouts fail
    /// <see cref="FrameLayout.SupportsWalkCycle"/> before any table is chosen.
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
            "Rapha", "Reis", "Simon", "Valmafra", "Zalmour"
        };

        /// <summary>
        /// The rig for <paramref name="characterName"/> (an Images/ folder name). Unknown names
        /// default to <see cref="WalkCycleType.Type1"/>: it is the majority rig and matches the
        /// pre-round-3 behavior, so a future roster addition degrades to the old (visible,
        /// reportable) arm placement instead of throwing.
        /// </summary>
        public static WalkCycleType For(string characterName)
        {
            return Type2Characters.Contains(characterName) ? WalkCycleType.Type2 : WalkCycleType.Type1;
        }
    }
}
