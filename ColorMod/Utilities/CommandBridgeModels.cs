using System.Collections.Generic;
using System.Text.Json.Serialization;
using FFTColorCustomizer.GameBridge;

namespace FFTColorCustomizer.Utilities
{
    public class CommandRequest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("slot")]
        public int Slot { get; set; } = -1;

        [JsonPropertyName("searchValue")]
        public int SearchValue { get; set; }

        [JsonPropertyName("searchLabel")]
        public string? SearchLabel { get; set; }

        [JsonPropertyName("fromLabel")]
        public string? FromLabel { get; set; }

        [JsonPropertyName("toLabel")]
        public string? ToLabel { get; set; }

        [JsonPropertyName("keys")]
        public List<KeyCommand> Keys { get; set; } = new();

        [JsonPropertyName("delayBetweenMs")]
        public int DelayBetweenMs { get; set; } = 150;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("steps")]
        public List<SequenceStep>? Steps { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("readSize")]
        public int ReadSize { get; set; } = 1;

        [JsonPropertyName("blockSize")]
        public int BlockSize { get; set; } = 0;

        [JsonPropertyName("addresses")]
        public List<BatchReadEntry>? Addresses { get; set; }

        /// <summary>
        /// Hex string of bytes to search for (e.g. "080B" for bytes 0x08 0x0B).
        /// Used with action "search_bytes".
        /// </summary>
        [JsonPropertyName("pattern")]
        public string? Pattern { get; set; }

        /// <summary>
        /// When true, scan_move returns full tile lists for every ability.
        /// When false (default), only occupied tiles are included + a totalTargets count.
        /// </summary>
        [JsonPropertyName("verbose")]
        public bool Verbose { get; set; }

        /// <summary>
        /// Target screen for "navigate" action (e.g. "PartyMenu", "JobScreen").
        /// </summary>
        [JsonPropertyName("to")]
        public string? To { get; set; }

        /// <summary>
        /// Target location ID for "travel" action.
        /// </summary>
        [JsonPropertyName("locationId")]
        public int LocationId { get; set; } = -1;

        /// <summary>
        /// Unit index for "navigate" action (which unit to select in party menu).
        /// </summary>
        [JsonPropertyName("unitIndex")]
        public int UnitIndex { get; set; } = 0;

        /// <summary>
        /// Direction for "battle_attack" action (e.g. "up", "down", "left", "right").
        /// </summary>
        [JsonPropertyName("direction")]
        public string? Direction { get; set; }

        /// <summary>
        /// After sending keys, poll DetectScreen until screen.Name matches this value (or timeout).
        /// Response is only sent once the screen matches, so the client gets guaranteed settled state.
        /// </summary>
        [JsonPropertyName("waitForScreen")]
        public string? WaitForScreen { get; set; }

        /// <summary>
        /// After sending keys, poll DetectScreen until screen.Name differs from this value (or timeout).
        /// Use when you know the current screen but not the target — e.g., "wait until we leave WorldMap".
        /// </summary>
        [JsonPropertyName("waitUntilScreenNot")]
        public string? WaitUntilScreenNot { get; set; }

        /// <summary>
        /// After sending keys, poll until any of the specified memory addresses change from their
        /// pre-key values. Use when screen name doesn't change but state does (e.g., cursor moved).
        /// </summary>
        [JsonPropertyName("waitForChange")]
        public List<string>? WaitForChange { get; set; }

        /// <summary>
        /// Timeout in ms for waitForScreen/waitUntilScreenNot/waitForChange. Default 2000ms.
        /// </summary>
        [JsonPropertyName("waitTimeoutMs")]
        public int WaitTimeoutMs { get; set; } = 2000;
    }

    public class BatchReadEntry
    {
        [JsonPropertyName("addr")]
        public string Addr { get; set; } = "";

        [JsonPropertyName("size")]
        public int Size { get; set; } = 1;

        [JsonPropertyName("label")]
        public string? Label { get; set; }
    }

    public class KeyCommand
    {
        [JsonPropertyName("vk")]
        public int Vk { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("holdMs")]
        public int HoldMs { get; set; } = 0;
    }

    public class CommandResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [JsonPropertyName("keysProcessed")]
        public int KeysProcessed { get; set; }

        [JsonPropertyName("keyResults")]
        public List<KeyResult> KeyResults { get; set; } = new();

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("info")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Info { get; set; }

        /// <summary>
        /// Emitted when the bridge auto-delayed a game command because it arrived
        /// too quickly after the previous one (chained-command rate limit). The
        /// command still ran — but if this appears, the caller chained commands
        /// with `&&` or similar when they should have used a `keys:[...]` batch.
        /// Value is a human-readable note (e.g. "auto-delayed 180ms; use keys:[...] batch for multi-key flows").
        /// </summary>
        [JsonPropertyName("chainWarning")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ChainWarning { get; set; }

        [JsonPropertyName("processedAt")]
        public string ProcessedAt { get; set; } = "";

        [JsonPropertyName("gameWindowFound")]
        public bool GameWindowFound { get; set; }

        [JsonPropertyName("gameState")]
        public GameState? GameState { get; set; }

        [JsonPropertyName("sequence")]
        public SequenceResult? Sequence { get; set; }

        [JsonPropertyName("readResult")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ReadResult? ReadResult { get; set; }

        [JsonPropertyName("blockData")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? BlockData { get; set; }

        [JsonPropertyName("reads")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<BatchReadResult>? Reads { get; set; }

        [JsonPropertyName("screen")]
        public DetectedScreen? Screen { get; set; }

        [JsonPropertyName("battle")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GameBridge.BattleState? Battle { get; set; }

        [JsonPropertyName("validPaths")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, GameBridge.PathEntry>? ValidPaths { get; set; }

        [JsonPropertyName("dialogue")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Dialogue { get; set; }

        /// <summary>
        /// Post-action snapshot of the active unit's state — position, HP, MP.
        /// Populated after successful battle_move, battle_attack, and battle_ability
        /// so Claude can confirm the action worked without a full rescan.
        /// </summary>
        [JsonPropertyName("postAction")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public PostActionState? PostAction { get; set; }

        /// <summary>
        /// Structured unit data from scan_units. Each entry has position, stats,
        /// team, class name, status effects, etc. Populated by scan_units and scan_move.
        /// </summary>
        [JsonPropertyName("units")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ScannedUnitResponse>? Units { get; set; }
    }

    /// <summary>JSON-serializable unit data from scan_units.</summary>
    public class ScannedUnitResponse
    {
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; }

        [JsonPropertyName("class")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Class { get; set; }

        [JsonPropertyName("team")]
        public string Team { get; set; } = "ENEMY";

        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("hp")]
        public int Hp { get; set; }

        [JsonPropertyName("maxHp")]
        public int MaxHp { get; set; }

        [JsonPropertyName("mp")]
        public int Mp { get; set; }

        [JsonPropertyName("maxMp")]
        public int MaxMp { get; set; }

        [JsonPropertyName("pa")]
        public int Pa { get; set; }

        [JsonPropertyName("ma")]
        public int Ma { get; set; }

        [JsonPropertyName("speed")]
        public int Speed { get; set; }

        [JsonPropertyName("ct")]
        public int Ct { get; set; }

        [JsonPropertyName("brave")]
        public int Brave { get; set; }

        [JsonPropertyName("faith")]
        public int Faith { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("exp")]
        public int Exp { get; set; }

        [JsonPropertyName("move")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Move { get; set; }

        [JsonPropertyName("jump")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Jump { get; set; }

        [JsonPropertyName("isActive")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsActive { get; set; }

        [JsonPropertyName("statuses")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Statuses { get; set; }

        [JsonPropertyName("reaction")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Reaction { get; set; }

        [JsonPropertyName("support")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Support { get; set; }

        [JsonPropertyName("movement")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Movement { get; set; }

        [JsonPropertyName("lifeState")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LifeState { get; set; }

        /// <summary>
        /// For the active unit: abilities with their valid target tile coordinates.
        /// Each ability lists the exact tiles it can reach from the unit's current position.
        /// Only populated for the active unit via scan_move.
        /// </summary>
        [JsonPropertyName("abilities")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<AbilityWithTiles>? Abilities { get; set; }
    }

    /// <summary>An ability with its reachable target tile coordinates.</summary>
    public class AbilityWithTiles
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("range")]
        public int Range { get; set; }

        [JsonPropertyName("mpCost")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int MpCost { get; set; }

        [JsonPropertyName("targets")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<int[]>? Targets { get; set; }  // [[x,y], [x,y], ...] compact format
    }

    /// <summary>
    /// Lightweight snapshot of the active unit's state after a battle action
    /// completes. Read from the condensed struct — faster than a full scan.
    /// </summary>
    public class PostActionState
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("hp")]
        public int Hp { get; set; }

        [JsonPropertyName("maxHp")]
        public int MaxHp { get; set; }

        [JsonPropertyName("mp")]
        public int Mp { get; set; }

        [JsonPropertyName("maxMp")]
        public int MaxMp { get; set; }
    }

    public class BatchReadResult
    {
        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("addr")]
        public string Addr { get; set; } = "";

        [JsonPropertyName("val")]
        public long Val { get; set; }

        [JsonPropertyName("hex")]
        public string Hex { get; set; } = "";
    }

    public class DetectedScreen
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "Unknown";

        [JsonPropertyName("uiPresent")]
        public int UiPresent { get; set; }

        [JsonPropertyName("location")]
        public int Location { get; set; }

        [JsonPropertyName("locationName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LocationName { get; set; }

        [JsonPropertyName("hover")]
        public int Hover { get; set; }

        [JsonPropertyName("menuCursor")]
        public int MenuCursor { get; set; }

        /// <summary>
        /// Party-menu panel depth discriminator at memory address 0x14077CB67.
        /// 0 = outer menus (WorldMap / PartyMenu / CharacterStatus),
        /// 2 = inner panels (EquipmentAndAbilities / ability picker).
        /// Discovered 2026-04-14 session 13. Primary use: drift-check for
        /// the state machine — if the state machine believes we're on an
        /// inner panel but this reads 0, we're actually on an outer screen
        /// and should snap back. Readers should treat this as a hint, not
        /// a full screen-name resolver (outer/inner only).
        /// </summary>
        [JsonPropertyName("menuDepth")]
        public int MenuDepth { get; set; }

        [JsonPropertyName("ui")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UI { get; set; }

        [JsonPropertyName("hoveredAction")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? HoveredAction { get; set; }

        [JsonPropertyName("battleTeam")]
        public int BattleTeam { get; set; }

        [JsonPropertyName("battleActed")]
        public int BattleActed { get; set; }

        [JsonPropertyName("battleMoved")]
        public int BattleMoved { get; set; }

        [JsonPropertyName("battleUnitId")]
        public int BattleUnitId { get; set; }

        [JsonPropertyName("battleUnitHp")]
        public int BattleUnitHp { get; set; }

        /// <summary>Cursor tile X during BattleMoving/BattleAttacking.</summary>
        [JsonPropertyName("cursorX")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int CursorX { get; set; } = -1;

        /// <summary>Cursor tile Y during BattleMoving/BattleAttacking.</summary>
        [JsonPropertyName("cursorY")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int CursorY { get; set; } = -1;

        /// <summary>Available tiles during BattleMoving (list of X,Y pairs).</summary>
        [JsonPropertyName("tiles")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<TilePosition>? Tiles { get; set; }

        /// <summary>Camera rotation (0-3). Affects which direction arrow keys move the cursor on the isometric grid.</summary>
        [JsonPropertyName("cameraRotation")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int CameraRotation { get; set; } = -1;

        /// <summary>Event script ID during cutscenes (e.g. 2=Orbonne, 4=first battle). 0 when not in cutscene.</summary>
        [JsonPropertyName("eventId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int EventId { get; set; }

        /// <summary>Story objective location ID (yellow diamond on world map). 0 when no objective.</summary>
        [JsonPropertyName("storyObjective")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int StoryObjective { get; set; }

        /// <summary>Human-readable name for the story objective location.</summary>
        [JsonPropertyName("storyObjectiveName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? StoryObjectiveName { get; set; }

        /// <summary>Player's current gil (currency). u32 at static 0x140D39CD0.
        /// Omitted when zero since that usually indicates a failed read.</summary>
        [JsonPropertyName("gil")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long Gil { get; set; }

        /// <summary>Row index of the highlighted item inside OutfitterBuy/Sell/Fitting.
        /// u32 at static 0x141870704. 0-based, increments per ScrollDown.
        /// null = not applicable (screens outside the Outfitter sub-actions).</summary>
        [JsonPropertyName("shopListCursorIndex")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? ShopListCursorIndex { get; set; }

        /// <summary>
        /// Location IDs that are currently unlocked/revealed for travel.
        /// Populated on WorldMap and TravelList screens only — lets Claude
        /// plan routes without guessing which nodes are available. Sourced
        /// from the per-location unlock array at 0x1411A10B0 (1 byte each;
        /// 0x01 = unlocked). Omitted on every other screen.
        /// </summary>
        [JsonPropertyName("unlockedLocations")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int[]? UnlockedLocations { get; set; }

        /// <summary>
        /// Player inventory. Every item the party owns with a non-zero
        /// count, pulled from the static u8 array at 0x1411A17C0. Each
        /// entry has itemId, count, and name/type from ItemData.cs.
        /// Populated on PartyMenuInventory (and will expand to shop
        /// screens + equipment pickers in follow-up work).
        /// Null on screens where inventory is not relevant.
        /// </summary>
        [JsonPropertyName("inventory")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<InventoryItem>? Inventory { get; set; }

        /// <summary>Name of the active unit (e.g. "Ramza"). Null for generic units without names.</summary>
        [JsonPropertyName("activeUnitName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ActiveUnitName { get; set; }

        /// <summary>Job name of the active unit (e.g. "Archer", "Gallant Knight").</summary>
        [JsonPropertyName("activeUnitJob")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ActiveUnitJob { get; set; }

        /// <summary>CharacterStatus view toggle: `1` key expands the full stat grid
        /// (Move/Jump/PA/MA/PE/ME/weapon-parry/shield-parry/cloak-evade). When true,
        /// the game's hint reads "[1] Less" and the header shows all numeric stats.
        /// Only emitted on CharacterStatus.</summary>
        [JsonPropertyName("statsExpanded")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool StatsExpanded { get; set; }

        /// <summary>EquipmentAndAbilities view toggle: `R` key flips between the
        /// default two-column equipment+abilities list and an "Equipment Effects"
        /// summary of aggregate stat effects. Only emitted on EquipmentAndAbilities.</summary>
        [JsonPropertyName("equipmentEffectsView")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool EquipmentEffectsView { get; set; }

        /// <summary>Full party roster grid surfaced on the PartyMenu Units tab.
        /// Omitted on every other screen. Columns fixed at 5; rows flex with
        /// roster size. See RosterReader and TODO §10.6.</summary>
        [JsonPropertyName("roster")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public RosterGrid? Roster { get; set; }

        /// <summary>Equipped items for the unit currently shown on
        /// EquipmentAndAbilities / CharacterStatus. Only populated when we
        /// can identify the viewed unit (currently Ramza-only — see TODO
        /// §10.6 "Viewed-unit slot address").</summary>
        [JsonPropertyName("loadout")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Loadout? Loadout { get; set; }

        /// <summary>Equipped abilities (Primary/Secondary/Reaction/Support/
        /// Movement) for the unit currently shown on EquipmentAndAbilities.
        /// Only populated when we can identify the viewed unit (currently
        /// Ramza-only — see TODO §10.6).</summary>
        [JsonPropertyName("abilities")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AbilityLoadoutPayload? Abilities { get; set; }

        /// <summary>Available choices for an ability picker screen
        /// (SecondaryAbilities / ReactionAbilities / etc.). Each entry is a
        /// name the player could equip in that slot. The currently-equipped
        /// entry is flagged via IsEquipped. Only populated on picker screens.</summary>
        [JsonPropertyName("availableAbilities")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<AvailableAbility>? AvailableAbilities { get; set; }

        /// <summary>Detail panel for whatever the cursor is hovering in the
        /// EquipmentAndAbilities screen or a picker. Mirrors the game's
        /// right-side info panel so Claude can make decisions like a real
        /// player (e.g. compare WP/evade, read ability descriptions).</summary>
        [JsonPropertyName("uiDetail")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public UiDetail? UiDetail { get; set; }

        /// <summary>Cursor row (0-based) on grid/list screens. Only populated
        /// when the screen has a meaningful row cursor (EquipmentAndAbilities).</summary>
        [JsonPropertyName("cursorRow")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? CursorRow { get; set; }

        /// <summary>Cursor column (0-based) on grid screens.</summary>
        [JsonPropertyName("cursorCol")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? CursorCol { get; set; }

        /// <summary>
        /// For JobSelection only: the three-state classification of the
        /// currently-hovered cell. "Locked" = no party member has the class
        /// (shadow silhouette); "Visible" = someone in the party has it but
        /// the viewed unit doesn't meet prereqs (change refused);
        /// "Unlocked" = viewed unit can change to this class. Omitted on
        /// other screens.
        /// </summary>
        [JsonPropertyName("jobCellState")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? JobCellState { get; set; }

        /// <summary>
        /// Name of the unit whose nested PartyMenu panel is currently
        /// shown. Populated on unit-scoped screens: CharacterStatus,
        /// EquipmentAndAbilities, JobSelection, ability pickers,
        /// CombatSets, CharacterDialog, DismissUnit. Omitted on PartyMenu
        /// itself, WorldMap, battle screens. Makes it possible to know
        /// "whose ui= is this?" without correlating back to key history.
        /// </summary>
        [JsonPropertyName("viewedUnit")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ViewedUnit { get; set; }

        /// <summary>
        /// Name of the item currently equipped in the slot this picker was
        /// opened for. Populated on EquippableWeapons / Shields / Headware
        /// / CombatGarb / Accessories pickers only. Helps Claude compare
        /// "what I have now" against the hovered alternative without
        /// jumping back to EquipmentAndAbilities.
        /// </summary>
        [JsonPropertyName("equippedItem")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? EquippedItem { get; set; }

        /// <summary>
        /// Active picker-tab display name on EquippableItemList pickers.
        /// Examples: "Equippable Weapons" / "Equippable Shields" /
        /// "All Weapons &amp; Shields" (R/L Hand), or "Equippable Headwear" /
        /// "All Headwear" (Helm slot), etc. A/D keys cycle tabs; wraps.
        /// </summary>
        [JsonPropertyName("pickerTab")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PickerTab { get; set; }
    }

    public class UiDetail
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>Category label — e.g. "Knight's Sword", "Reaction", "Support", "Primary skillset".</summary>
        [JsonPropertyName("type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Type { get; set; }

        /// <summary>Source job for passive abilities / primary skillset owner.</summary>
        [JsonPropertyName("job")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Job { get; set; }

        /// <summary>Weapon power (for weapons).</summary>
        [JsonPropertyName("wp")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Wp { get; set; }

        /// <summary>Weapon evade % (for weapons).</summary>
        [JsonPropertyName("wev")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Wev { get; set; }

        /// <summary>Weapon range (for weapons).</summary>
        [JsonPropertyName("range")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Range { get; set; }

        /// <summary>Physical evade % (for shields/cloaks).</summary>
        [JsonPropertyName("pev")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Pev { get; set; }

        /// <summary>Magic evade % (for shields/cloaks).</summary>
        [JsonPropertyName("mev")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Mev { get; set; }

        /// <summary>HP bonus (for armor/helm).</summary>
        [JsonPropertyName("hpBonus")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int HpBonus { get; set; }

        /// <summary>MP bonus (for armor/helm).</summary>
        [JsonPropertyName("mpBonus")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int MpBonus { get; set; }

        /// <summary>Free-form description text.</summary>
        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Description { get; set; }

        /// <summary>Optional "Usage Conditions" text shown by the game for
        /// passive abilities that have a trigger condition (e.g. Mana Shield:
        /// "Activates when HP loss is 1 or more"). Extracted from the tail of
        /// Description when the ability data has a "Usage condition:" marker.</summary>
        [JsonPropertyName("usageCondition")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UsageCondition { get; set; }

        /// <summary>
        /// Additive stat modifiers granted by the item while equipped —
        /// mirrors page 2 "Attribute Bonuses" of the in-game item info
        /// panel (e.g. "PA+1", "MA+2", "Speed+1", "PA+1, MA+1").
        /// </summary>
        [JsonPropertyName("attributeBonuses")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AttributeBonuses { get; set; }

        /// <summary>
        /// Passive effects active while the item is equipped — mirrors
        /// "Equipment Effects" (e.g. "Auto-Haste", "Permanent Shell",
        /// "Auto-Reraise", "Immune Blindness").
        /// </summary>
        [JsonPropertyName("equipmentEffects")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? EquipmentEffects { get; set; }

        /// <summary>
        /// Weapon-only: side effects of basic attacks with this weapon
        /// (e.g. "On hit: chance to add Stone", "Drain HP on hit").
        /// </summary>
        [JsonPropertyName("attackEffects")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AttackEffects { get; set; }

        /// <summary>
        /// Weapon-only: elemental property the weapon adds to basic
        /// attacks (e.g. "Holy" on Excalibur).
        /// </summary>
        [JsonPropertyName("element")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Element { get; set; }

        /// <summary>
        /// Weapon-only: weapon can be paired via Dual Wield support.
        /// </summary>
        [JsonPropertyName("canDualWield")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool CanDualWield { get; set; }

        /// <summary>
        /// Weapon-only: weapon is compatible with Doublehand support
        /// (double ATK on basic attacks when main-hand + no off-hand).
        /// </summary>
        [JsonPropertyName("canWieldTwoHanded")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool CanWieldTwoHanded { get; set; }
    }

    public class AvailableAbility
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("isEquipped")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsEquipped { get; set; }

        /// <summary>
        /// Source job for this ability (e.g. "Monk" for Martial Arts,
        /// "Ninja" for Dual Wield). Populated on ability pickers so Claude
        /// sees "which class do I level to unlock more of this type?"
        /// without cross-referencing. Null when the ability has no single
        /// owning job (e.g. generic skillsets).
        /// </summary>
        [JsonPropertyName("job")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Job { get; set; }

        /// <summary>
        /// Short description of the ability (one sentence / short blurb).
        /// Populated on ability pickers so Claude can compare options
        /// without opening each detail panel in sequence.
        /// </summary>
        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Description { get; set; }
    }

    public class AbilityLoadoutPayload
    {
        [JsonPropertyName("primary")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Primary { get; set; }

        [JsonPropertyName("secondary")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Secondary { get; set; }

        [JsonPropertyName("reaction")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Reaction { get; set; }

        [JsonPropertyName("support")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Support { get; set; }

        [JsonPropertyName("movement")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Movement { get; set; }

        /// <summary>
        /// Skillsets the viewed unit has unlocked (at least one action
        /// ability learned in that job). Canonical order. Each entry can be
        /// equipped in the Secondary slot. Populated on EquipmentAndAbilities
        /// so the fft.sh `list_secondary_abilities` helper doesn't have to
        /// open the Secondary picker just to read it.
        /// </summary>
        [JsonPropertyName("learnedSecondary")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? LearnedSecondary { get; set; }

        /// <summary>Reaction abilities the viewed unit has learned.</summary>
        [JsonPropertyName("learnedReaction")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? LearnedReaction { get; set; }

        /// <summary>Support abilities the viewed unit has learned.</summary>
        [JsonPropertyName("learnedSupport")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? LearnedSupport { get; set; }

        /// <summary>Movement abilities the viewed unit has learned.</summary>
        [JsonPropertyName("learnedMovement")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? LearnedMovement { get; set; }
    }

    public class Loadout
    {
        [JsonPropertyName("unitName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UnitName { get; set; }

        [JsonPropertyName("weapon")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Weapon { get; set; }

        [JsonPropertyName("leftHand")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LeftHand { get; set; }

        [JsonPropertyName("shield")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Shield { get; set; }

        [JsonPropertyName("helm")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Helm { get; set; }

        [JsonPropertyName("body")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Body { get; set; }

        [JsonPropertyName("accessory")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Accessory { get; set; }
    }

    /// <summary>
    /// One entry in the player's owned-items inventory. Sourced from the
    /// static u8 array at 0x1411A17C0 where each byte is the count for
    /// the item whose FFTPatcher canonical ID equals the byte's index.
    /// Name/type pulled from ItemData.cs; null when we haven't mapped
    /// that ID yet.
    /// </summary>
    public class InventoryItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Type { get; set; }

        /// <summary>
        /// Gil amount this item sells for at an Outfitter. Null for items
        /// without a known buy price (story drops, unique equipment).
        /// Computed as BuyPrice / 2 via <see cref="GameBridge.ItemPrices"/>.
        /// </summary>
        [JsonPropertyName("sellPrice")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? SellPrice { get; set; }
    }

    /// <summary>
    /// Party roster surfaced on PartyMenu. Units are ordered by memory slot
    /// index, NOT by the game's on-screen display order — verified 2026-04-14
    /// that the game reorders story characters onto the 5-col grid via a
    /// separate display-order list we haven't located yet. Consumers should
    /// treat `slot` as the canonical ID and ignore position-in-list.
    /// </summary>
    public class RosterGrid
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("max")]
        public int Max { get; set; } = 50;

        [JsonPropertyName("units")]
        public List<RosterUnit> Units { get; set; } = new();

        /// <summary>
        /// PartyMenu grid width in columns. Always 5 for the Units tab; null
        /// when the grid isn't applicable (e.g. non-PartyMenu screens that
        /// still carry the roster list for reference).
        /// </summary>
        [JsonPropertyName("gridCols")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? GridCols { get; set; }

        /// <summary>
        /// Grid rows needed to render all `Count` units at `GridCols` width.
        /// </summary>
        [JsonPropertyName("gridRows")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? GridRows { get; set; }

        /// <summary>
        /// 0-indexed cursor row within the Units grid (only set on PartyMenu
        /// Units tab). Mirrors CursorRow in the state machine.
        /// </summary>
        [JsonPropertyName("cursorRow")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? CursorRow { get; set; }

        /// <summary>
        /// 0-indexed cursor column within the Units grid.
        /// </summary>
        [JsonPropertyName("cursorCol")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? CursorCol { get; set; }

        /// <summary>
        /// Name of the unit currently highlighted by the cursor (convenience
        /// for consumers; equivalent to Units.First(u =&gt; u.DisplayOrder ==
        /// CursorRow*GridCols + CursorCol).Name).
        /// </summary>
        [JsonPropertyName("hoveredName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? HoveredName { get; set; }
    }

    public class RosterUnit
    {
        [JsonPropertyName("slot")]
        public int Slot { get; set; }

        [JsonPropertyName("hp")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Hp { get; set; }

        [JsonPropertyName("maxHp")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int MaxHp { get; set; }

        [JsonPropertyName("mp")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Mp { get; set; }

        [JsonPropertyName("maxMp")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int MaxMp { get; set; }

        [JsonPropertyName("equipment")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Loadout? Equipment { get; set; }

        /// <summary>JP earned in the unit's currently-equipped class.
        /// Read from roster +0x80 (u16). Verified 2026-04-14 live: Ramza
        /// on Gallant Knight = 9999, Mustadio on Machinist = 152. The
        /// broader 22-job JP array starts at +0x82 and is deferred to a
        /// later pass (job-index mapping needs nailing down first).</summary>
        [JsonPropertyName("jp")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Jp { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("job")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Job { get; set; }

        [JsonPropertyName("brave")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Brave { get; set; }

        [JsonPropertyName("faith")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Faith { get; set; }

        /// <summary>
        /// 0-indexed position within the PartyMenu Units grid (display order,
        /// driven by the game's Sort option — default Time Recruited). Read
        /// from roster +0x122. This is what cursorRow*gridCols + cursorCol
        /// resolves to. Unlike `slot` this reflects what the player sees
        /// visually.
        /// </summary>
        [JsonPropertyName("displayOrder")]
        public int DisplayOrder { get; set; }
    }

    public class TilePosition
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("h")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public double H { get; set; }
    }

    public class SequenceStep
    {
        [JsonPropertyName("keys")]
        public List<KeyCommand> Keys { get; set; } = new();

        [JsonPropertyName("waitMs")]
        public int WaitMs { get; set; } = 0;

        [JsonPropertyName("assert")]
        public SequenceAssert? Assert { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("readAddress")]
        public string? ReadAddress { get; set; }

        [JsonPropertyName("readSize")]
        public int ReadSize { get; set; } = 1;

        [JsonPropertyName("waitForScreen")]
        public string? WaitForScreen { get; set; }

        [JsonPropertyName("waitUntilScreenNot")]
        public string? WaitUntilScreenNot { get; set; }

        [JsonPropertyName("waitForChange")]
        public List<string>? WaitForChange { get; set; }

        [JsonPropertyName("waitTimeoutMs")]
        public int WaitTimeoutMs { get; set; } = 2000;
    }

    public class SequenceAssert
    {
        [JsonPropertyName("screen")]
        public string? Screen { get; set; }

        [JsonPropertyName("cursorIndex")]
        public int? CursorIndex { get; set; }

        [JsonPropertyName("tab")]
        public string? Tab { get; set; }

        [JsonPropertyName("sidebarIndex")]
        public int? SidebarIndex { get; set; }
    }

    public class SequenceResult
    {
        [JsonPropertyName("stepsCompleted")]
        public int StepsCompleted { get; set; }

        [JsonPropertyName("totalSteps")]
        public int TotalSteps { get; set; }

        [JsonPropertyName("stepResults")]
        public List<StepResult> StepResults { get; set; } = new();

        [JsonPropertyName("failedAssertion")]
        public AssertionFailure? FailedAssertion { get; set; }
    }

    public class StepResult
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("keysProcessed")]
        public int KeysProcessed { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [JsonPropertyName("readResult")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ReadResult? ReadResult { get; set; }
    }

    public class AssertionFailure
    {
        [JsonPropertyName("stepIndex")]
        public int StepIndex { get; set; }

        [JsonPropertyName("field")]
        public string Field { get; set; } = "";

        [JsonPropertyName("expected")]
        public string Expected { get; set; } = "";

        [JsonPropertyName("actual")]
        public string Actual { get; set; } = "";
    }

    public class KeyResult
    {
        [JsonPropertyName("vk")]
        public int Vk { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }

    public class ReadResult
    {
        [JsonPropertyName("address")]
        public string Address { get; set; } = "";

        [JsonPropertyName("size")]
        public int Size { get; set; }

        [JsonPropertyName("value")]
        public long Value { get; set; }

        [JsonPropertyName("hex")]
        public string Hex { get; set; } = "";

        [JsonPropertyName("rawBytes")]
        public string RawBytes { get; set; } = "";
    }
}
