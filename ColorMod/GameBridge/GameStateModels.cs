using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FFTColorCustomizer.GameBridge
{
    public class GameState
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = "";

        [JsonPropertyName("scanStatus")]
        public string ScanStatus { get; set; } = "not_initialized";

        [JsonPropertyName("unitDataBaseAddress")]
        public string UnitDataBaseAddress { get; set; } = "0x0";

        [JsonPropertyName("activeUnitCount")]
        public int ActiveUnitCount { get; set; }

        [JsonPropertyName("ui")]
        public UIState? UI { get; set; }

        [JsonPropertyName("units")]
        public List<UnitState> Units { get; set; } = new();

        [JsonPropertyName("screenState")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ScreenState? ScreenState { get; set; }
    }

    public class UIState
    {
        [JsonPropertyName("cursorIndex")]
        public int CursorIndex { get; set; }

        [JsonPropertyName("selectedHp")]
        public int SelectedHp { get; set; }

        [JsonPropertyName("selectedMaxHp")]
        public int SelectedMaxHp { get; set; }

        [JsonPropertyName("selectedMp")]
        public int SelectedMp { get; set; }

        [JsonPropertyName("selectedMaxMp")]
        public int SelectedMaxMp { get; set; }

        [JsonPropertyName("selectedJob")]
        public int SelectedJob { get; set; }

        [JsonPropertyName("selectedBrave")]
        public int SelectedBrave { get; set; }

        [JsonPropertyName("selectedFaith")]
        public int SelectedFaith { get; set; }
    }

    public class UnitState
    {
        [JsonPropertyName("slot")]
        public int Slot { get; set; }

        [JsonPropertyName("spriteSet")]
        public int SpriteSet { get; set; }

        [JsonPropertyName("unitIndex")]
        public int UnitIndex { get; set; }

        [JsonPropertyName("job")]
        public int Job { get; set; }

        [JsonPropertyName("jobName")]
        public string? JobName { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("experience")]
        public int Experience { get; set; }

        [JsonPropertyName("brave")]
        public int Brave { get; set; }

        [JsonPropertyName("faith")]
        public int Faith { get; set; }

        [JsonPropertyName("nameId")]
        public int NameId { get; set; }

        [JsonPropertyName("gridPosition")]
        public int GridPosition { get; set; }

        [JsonPropertyName("secondaryAbility")]
        public int SecondaryAbility { get; set; }

        [JsonPropertyName("reactionAbility")]
        public int ReactionAbility { get; set; }

        [JsonPropertyName("reactionAbilityName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ReactionAbilityName { get; set; }

        [JsonPropertyName("supportAbility")]
        public int SupportAbility { get; set; }

        [JsonPropertyName("supportAbilityName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SupportAbilityName { get; set; }

        [JsonPropertyName("movementAbility")]
        public int MovementAbility { get; set; }

        [JsonPropertyName("movementAbilityName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MovementAbilityName { get; set; }
    }
}
