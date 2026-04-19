using System.Collections.Generic;
using System.IO;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Decodes FFT .mes (message) files using PSX text encoding.
    /// These files contain cutscene dialogue with speaker tags and control codes.
    /// </summary>
    public static class MesDecoder
    {
        public record DialogueLine(string? Speaker, string Text);

        // One in-game text bubble. Boxes are the unit the user advances through
        // with Enter.
        public record DialogueBox(string? Speaker, string Text);

        /// <summary>
        /// Decode raw .mes bytes into in-game dialogue BOXES (one per Enter-advance).
        /// <para>Byte roles (verified by live walk-through of Dorter event 38 on
        /// 2026-04-19: 45 real bubbles, 45 0xFE boundaries in the file):</para>
        /// <list type="bullet">
        /// <item><c>0xFE</c> = bubble boundary (one Enter-advance per run).</item>
        /// <item><c>0xF8</c> = intra-bubble line wrap (visual newline).</item>
        /// <item>Consecutive <c>0xFE</c> bytes collapse into ONE boundary
        /// (the game uses runs of 2-5 FE bytes for pause/animation beats).</item>
        /// <item>Speaker change (<c>0xE3 0x08 ... 0xE3 0x00</c>) is an implicit
        /// bubble boundary even without a preceding <c>0xFE</c>.</item>
        /// </list>
        /// </summary>
        public static List<DialogueBox> DecodeBoxes(byte[] bytes)
        {
            var boxes = new List<DialogueBox>();
            string? currentSpeaker = null;
            var text = new System.Text.StringBuilder();

            void Flush()
            {
                var t = text.ToString().TrimEnd();
                if (t.Length > 0) boxes.Add(new DialogueBox(currentSpeaker, t));
                text.Clear();
            }

            int i = 0;
            while (i < bytes.Length)
            {
                byte b = bytes[i];

                if (b == 0xE3 && i + 1 < bytes.Length && bytes[i + 1] == 0x08)
                {
                    // Speaker change is an implicit bubble boundary.
                    Flush();
                    i += 2;
                    var nameBuilder = new System.Text.StringBuilder();
                    while (i < bytes.Length && bytes[i] != 0xE3)
                    {
                        var c = DecodeByte(bytes[i]);
                        if (c.HasValue) nameBuilder.Append(c.Value);
                        i++;
                    }
                    if (i < bytes.Length) i++;
                    if (i < bytes.Length) i++;
                    currentSpeaker = nameBuilder.ToString();
                    continue;
                }

                if (b == 0xFE)
                {
                    // Any run of 0xFE bytes = one bubble boundary.
                    while (i < bytes.Length && bytes[i] == 0xFE) i++;
                    Flush();
                    continue;
                }

                if (b == 0xF8)
                {
                    // Intra-bubble line wrap.
                    text.Append('\n');
                    i++;
                    continue;
                }

                if (b == 0xE2) { i += 2; continue; }
                if (b == 0xE3) { i += 2; continue; }

                var ch = DecodeByte(b);
                if (ch.HasValue) text.Append(ch.Value);
                i++;
            }
            Flush();
            return boxes;
        }

        /// <summary>
        /// Decode a single byte using FFT PSX text encoding.
        /// Returns the character, or null for control/unknown bytes.
        /// </summary>
        public static char? DecodeByte(byte b)
        {
            if (b >= 0x00 && b <= 0x09) return (char)('0' + b);
            if (b >= 0x0A && b <= 0x23) return (char)('A' + b - 0x0A);
            if (b >= 0x24 && b <= 0x3D) return (char)('a' + b - 0x24);
            if (b == 0x88 || b == 0xFA) return ' ';
            if (b == 0x78 || b == 0x5F) return '.';
            if (b == 0x3E) return '!';
            if (b == 0x40 || b == 0x7B) return '?';
            if (b == 0x46) return ':';
            if (b == 0x8B) return ',';
            if (b == 0x3F || b == 0x7D) return '\'';
            if (b == 0x45) return ';';
            if (b == 0x47) return '"';
            if (b == 0x7C) return '\n';
            if (b == 0x44) return '-';
            if (b == 0x48) return '(';
            if (b == 0x49) return ')';
            if (b == 0xB7) return '/';
            return null;
        }

        /// <summary>
        /// Decode a byte array to a string using PSX text encoding.
        /// </summary>
        public static string DecodeBytes(byte[] bytes)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var b in bytes)
            {
                var c = DecodeByte(b);
                if (c.HasValue) sb.Append(c.Value);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Decode a .mes file into a list of dialogue lines with speaker tags.
        /// </summary>
        public static List<DialogueLine> DecodeFile(string path)
        {
            var bytes = File.ReadAllBytes(path);
            return DecodeBytes(bytes, out _);
        }

        /// <summary>
        /// Decode raw bytes into dialogue lines with speaker tags.
        /// </summary>
        public static List<DialogueLine> DecodeBytes(byte[] bytes, out string rawText)
        {
            var lines = new List<DialogueLine>();
            var sb = new System.Text.StringBuilder();
            string? currentSpeaker = null;
            var currentText = new System.Text.StringBuilder();

            int i = 0;
            while (i < bytes.Length)
            {
                byte b = bytes[i];

                // Speaker tag: E3 08 <name bytes> E3 <closing byte>
                if (b == 0xE3 && i + 1 < bytes.Length && bytes[i + 1] == 0x08)
                {
                    // Flush current text as a line
                    FlushLine(lines, currentSpeaker, currentText);

                    i += 2; // skip E3 08
                    var nameBuilder = new System.Text.StringBuilder();
                    while (i < bytes.Length && bytes[i] != 0xE3)
                    {
                        var c = DecodeByte(bytes[i]);
                        if (c.HasValue) nameBuilder.Append(c.Value);
                        i++;
                    }
                    if (i < bytes.Length) i++; // skip closing E3
                    if (i < bytes.Length) i++; // skip closing byte after E3
                    currentSpeaker = nameBuilder.ToString();
                    continue;
                }

                // New dialogue entry marker
                if (b == 0xF8)
                {
                    FlushLine(lines, currentSpeaker, currentText);
                    i++;
                    continue;
                }

                // Line break within same entry
                if (b == 0xFE)
                {
                    FlushLine(lines, currentSpeaker, currentText);
                    i++;
                    continue;
                }

                // Skip E2 control pairs
                if (b == 0xE2)
                {
                    i += 2;
                    continue;
                }

                // Skip other E3 variants (non-0x08)
                if (b == 0xE3)
                {
                    i += 2;
                    continue;
                }

                // Decode character
                var ch = DecodeByte(b);
                if (ch.HasValue)
                {
                    currentText.Append(ch.Value);
                    sb.Append(ch.Value);
                }
                else if (b > 0x7F)
                {
                    // Skip unknown control bytes
                }

                i++;
            }

            FlushLine(lines, currentSpeaker, currentText);
            rawText = sb.ToString();
            return lines;
        }

        private static void FlushLine(List<DialogueLine> lines, string? speaker, System.Text.StringBuilder text)
        {
            var t = text.ToString().Trim();
            if (t.Length > 0)
                lines.Add(new DialogueLine(speaker, t));
            text.Clear();
        }
    }
}
