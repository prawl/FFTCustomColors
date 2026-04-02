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
        EquipmentScreen,
        EquipmentItemList,
        JobScreen,
        JobActionMenu,
        JobChangeConfirmation
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
