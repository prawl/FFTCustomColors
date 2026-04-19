using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Loads and caches decoded event scripts from .mes files.
    /// Provides lookup by event ID to get dialogue for cutscenes.
    /// </summary>
    public class EventScriptLookup
    {
        /// <summary>
        /// Decoded event-script record.
        /// <para>HasChoice: true when the raw .mes bytes contain the 0xFB marker — a
        /// byte that ONLY appears in mid-story choice prompts (e.g. "1. Defeat the
        /// Brigade" / "2. Rescue the captive" at Mandalia Plain event 016). All
        /// observed non-choice events (2, 5, 6, 8, 10, 11, 12) have zero 0xFB
        /// bytes. This flag is the discriminator that separates BattleChoice from
        /// ordinary BattleDialogue/Cutscene when the bridge's memory-only
        /// detection can't tell them apart — session 44 2026-04-18 finding.</para>
        /// </summary>
        public record EventScript(int EventId, List<MesDecoder.DialogueLine> Lines, bool HasChoice, List<MesDecoder.DialogueBox> Boxes);

        private readonly Dictionary<int, EventScript> _scripts = new();
        private readonly string _mesDirectory;

        public EventScriptLookup(string mesDirectory)
        {
            _mesDirectory = mesDirectory;
            LoadAll();
        }

        private void LoadAll()
        {
            if (!Directory.Exists(_mesDirectory)) return;

            foreach (var file in Directory.GetFiles(_mesDirectory, "event*.en.mes"))
            {
                var filename = Path.GetFileNameWithoutExtension(file); // e.g. "event002.en"
                var numPart = filename.Replace("event", "").Replace(".en", "");
                if (int.TryParse(numPart, out int eventId))
                {
                    var bytes = File.ReadAllBytes(file);
                    var lines = MesDecoder.DecodeBytes(bytes, out _);
                    var boxes = MesDecoder.DecodeBoxes(bytes);
                    // 0xFB is the choice-prompt marker (verified empirically:
                    // event016 has it, events 2/5/6/8/10/11/12 do not).
                    bool hasChoice = System.Array.IndexOf(bytes, (byte)0xFB) >= 0;
                    _scripts[eventId] = new EventScript(eventId, lines, hasChoice, boxes);
                }
            }
        }

        /// <summary>
        /// Get the decoded script for an event ID. Returns null if not found.
        /// </summary>
        public EventScript? GetScript(int eventId)
        {
            return _scripts.TryGetValue(eventId, out var script) ? script : null;
        }

        /// <summary>
        /// Get a human-readable formatted script for an event ID.
        /// Format: "Speaker: dialogue text" per line.
        /// </summary>
        public string? GetFormattedScript(int eventId)
        {
            var script = GetScript(eventId);
            if (script == null) return null;

            var sb = new StringBuilder();
            string? lastSpeaker = null;

            foreach (var line in script.Lines)
            {
                if (line.Speaker != null && line.Speaker != lastSpeaker)
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.Append(line.Speaker).Append(": ");
                    lastSpeaker = line.Speaker;
                }
                else if (line.Speaker == null && lastSpeaker != null)
                {
                    sb.Append("  ");
                }

                sb.AppendLine(line.Text);
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Get count of loaded scripts.
        /// </summary>
        public int Count => _scripts.Count;
    }
}
