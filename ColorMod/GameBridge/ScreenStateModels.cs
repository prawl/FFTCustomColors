using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FFTColorCustomizer.GameBridge
{
    public enum GameScreen
    {
        Unknown,
        TitleScreen,
        WorldMap,
        PartyMenu,
        CharacterStatus,

        // EquipmentScreen is the two-column (Equipment | Abilities) inner screen.
        // Legacy name — surfaces as "EquipmentAndAbilities" via the CommandWatcher mapper.
        EquipmentScreen,

        // EquipmentItemList is a generic single-picker list for an equipment slot.
        // Legacy name — the per-slot-specific labels (EquippableWeapons / Shields /
        // Headware / CombatGarb / Accessories) are resolved at detection time from
        // the EquipmentScreen cursor position at Enter time (see EquipmentSlot enum).
        EquipmentItemList,

        // Ability slot pickers: reached by pressing Enter on the right column of
        // EquipmentAndAbilities. Which one fires depends on the abilities-column
        // cursor row at Enter time.
        //
        // Row 0 (Primary action) is LOCKED to the unit's current job — no picker
        // opens, so there is no corresponding GameScreen. Row 1 opens the
        // SecondaryAbilities picker (Items / Arts of War / Aim / Martial Arts /
        // White Magicks / Black Magicks / Time Magicks / etc.).
        SecondaryAbilities,
        ReactionAbilities,
        SupportAbilities,
        MovementAbilities,

        JobScreen,            // legacy name; surfaces as "JobSelection"
        JobActionMenu,
        JobChangeConfirmation,

        // Third sidebar item on CharacterStatus — placeholder, behaviour not yet
        // modelled beyond the screen boundary (no inner navigation).
        CombatSets,

        // Flavor-text dialog opened by pressing Space on CharacterStatus
        // (e.g. Kenrick's "My father is an arms merchant..." intro).
        // Only Enter advances; Escape is a no-op in this game's dialogs.
        CharacterDialog,

        // Post-hold-B confirmation on CharacterStatus. Left/Right toggles
        // Confirm/Back; cursor defaults to Back so blind Enter is safe.
        DismissUnit,

        // Chronicle tab nested screens. The Chronicle root itself surfaces
        // as "PartyMenuChronicle" via the PartyMenu Tab. These are entered
        // by pressing Enter on a tile in the 3-row Chronicle grid:
        //   Row 0 (3 cols): Encyclopedia / StateOfRealm / Events
        //   Row 1 (4 cols): Auracite / ReadingMaterials / Collection / Errands
        //   Row 2 (3 cols): Stratagems / Lessons / AkademicReport
        ChronicleEncyclopedia,
        ChronicleStateOfRealm,
        ChronicleEvents,
        ChronicleAuracite,
        ChronicleReadingMaterials,
        ChronicleCollection,
        ChronicleErrands,
        ChronicleStratagems,
        ChronicleLessons,
        ChronicleAkademicReport,

        // Options tab nested screens. The Options root surfaces as
        // "PartyMenuOptions" via the PartyMenu Tab. The 5-row vertical
        // list opens these on Enter:
        //   0 Save     → triggers Save flow (existing handling)
        //   1 Load     → triggers Load flow (existing handling)
        //   2 Settings → OptionsSettings (new nested screen)
        //   3 Return to Title → confirmation modal (TBD)
        //   4 Exit Game → confirmation modal (TBD)
        // Save/Load are handled by existing `save`/`load` actions and don't
        // need a dedicated GameScreen yet. Settings is an actual screen.
        OptionsSettings
    }

    /// <summary>
    /// Identifies which equipment slot is highlighted in the left column of
    /// EquipmentAndAbilities. Used at Enter time to route to the correct
    /// Equippable&lt;Type&gt; picker screen. Row order matches the game's
    /// left-column layout.
    /// </summary>
    public enum EquipmentSlot
    {
        Weapon = 0,
        Shield = 1,
        Headware = 2,
        CombatGarb = 3,
        Accessory = 4
    }

    /// <summary>
    /// Identifies which ability slot is highlighted in the right column of
    /// EquipmentAndAbilities. Row order matches the game's right-column layout.
    /// </summary>
    /// <remarks>
    /// Row 0 (PrimaryAction) is LOCKED — the unit's current job determines the
    /// primary skillset (e.g. "Mettle" for Gallant Knight). Pressing Enter on
    /// row 0 does nothing; change the unit's job via JobSelection to change
    /// this slot. Only rows 1-4 open pickers.
    /// </remarks>
    public enum AbilitySlot
    {
        PrimaryAction = 0,
        SecondaryAction = 1,
        Reaction = 2,
        Support = 3,
        Movement = 4
    }

    public enum PartyTab
    {
        Units = 0,
        Inventory = 1,
        Chronicle = 2,
        Options = 3
    }

    public class ScreenState
    {
        [JsonPropertyName("screen")]
        public string Screen { get; set; } = "unknown";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("cursorRow")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? CursorRow { get; set; }

        [JsonPropertyName("cursorCol")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? CursorCol { get; set; }

        [JsonPropertyName("tab")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Tab { get; set; }

        [JsonPropertyName("sidebarIndex")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? SidebarIndex { get; set; }

        [JsonPropertyName("gridColumns")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? GridColumns { get; set; }

        [JsonPropertyName("gridRows")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? GridRows { get; set; }

        [JsonPropertyName("validActions")]
        public List<ValidAction> ValidActions { get; set; } = new();
    }

    public class ValidAction
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = "";

        [JsonPropertyName("vk")]
        public int Vk { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("resultScreen")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ResultScreen { get; set; }
    }
}
