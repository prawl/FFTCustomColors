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
