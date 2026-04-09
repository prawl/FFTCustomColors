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

        /// <summary>Cursor tile X during Battle_Moving/Battle_Attacking.</summary>
        [JsonPropertyName("cursorX")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int CursorX { get; set; } = -1;

        /// <summary>Cursor tile Y during Battle_Moving/Battle_Attacking.</summary>
        [JsonPropertyName("cursorY")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int CursorY { get; set; } = -1;

        /// <summary>Available tiles during Battle_Moving (list of X,Y pairs).</summary>
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
