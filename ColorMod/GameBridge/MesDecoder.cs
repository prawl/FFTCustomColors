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

        // Player-name placeholder. The .mes file encodes the player's chosen
        // name as a single byte 0xE0. Verified at event 045 (Eagrose Castle):
        // "Might I pose a question, [E0]?" → "Might I pose a question, Ramza?".
        // TODO: wire up runtime read of the player's actual chosen name and
        // pass it through as an argument when callers care.
        public const string PlayerNamePlaceholder = "Ramza";

        // Em-dash placeholder. 0xDA 0x68 is a typographic em-dash inserted
        // by the renderer, not a name. Verified at event 045: Larg's line
        // "And let us not forget—they did save the marquis's life." and
        // Dycedarg's "Is your intent to live up to your name—or…". An
        // earlier reading treated this as a second player-name placeholder
        // and produced "forgetRamzathey" — corrected 2026-04-26.
        public const string EmDashPlaceholder = "—";

        /// <summary>
        /// Decode raw .mes bytes into in-game dialogue BOXES (one per Enter-advance).
        /// <para>Byte roles, refined 2026-04-26 after a live walk-through of
        /// event 045 (Eagrose Castle):</para>
        /// <list type="bullet">
        /// <item><c>0xFE</c> = end-of-segment marker. The game shows the
        /// segment text as exactly <c>N</c> bubbles, where <c>N</c> is the
        /// length of the trailing 0xFE run. Single FE → 1 bubble; FE×3 →
        /// 3 bubbles. The renderer fills bubbles in order, splitting the
        /// text at sentence boundaries until the bubble count matches.</item>
        /// <item><c>0xF8</c> = whitespace inside a bubble (paragraph break,
        /// not a bubble break). Both single and run-of-2+ map to "\n" in
        /// the decoded text — the renderer reflows them visually.</item>
        /// <item><c>0xE3 0x08 ... 0xE3 0x00</c> = speaker change at a scene
        /// transition. NOT a per-bubble portrait code; the .mes file does
        /// not encode mid-scene speaker changes (those come from the .evt
        /// event script, which the bridge currently doesn't parse).</item>
        /// <item><c>0xE0</c> and <c>0xDA 0x68</c> = player-name
        /// placeholders. See <see cref="PlayerNamePlaceholder"/>.</item>
        /// </list>
        /// </summary>
        public static List<DialogueBox> DecodeBoxes(byte[] bytes)
        {
            // First pass: collect FE-bound segments along with their FE-run
            // length, which determines how many bubbles to paginate the
            // segment text into.
            // Speaker tracking note: a segment's speaker is ONLY taken from
            // a 0xE3 0x08 marker that appears inside that segment. Inherited
            // speakers across FE boundaries produce false confidence — the
            // .mes encoding doesn't actually mean "same speaker continues",
            // mid-scene speaker changes come from the .evt event script we
            // don't currently parse. Emitting null lets fft.sh fall back to
            // the neutral "narrator" tag instead of lying.
            var segments = new List<(string? Speaker, string Text, int FeRun)>();
            string? currentSegmentSpeaker = null;
            var text = new System.Text.StringBuilder();

            void Flush(int feRun)
            {
                var t = text.ToString().TrimEnd();
                if (t.Length > 0)
                    segments.Add((currentSegmentSpeaker, t, feRun));
                text.Clear();
                // Reset segment-level speaker so the next segment doesn't
                // inherit it. The next 0xE3 0x08 marker (if any) will
                // re-set it for the new segment.
                currentSegmentSpeaker = null;
            }

            int i = 0;
            while (i < bytes.Length)
            {
                byte b = bytes[i];

                if (b == 0xE3 && i + 1 < bytes.Length && bytes[i + 1] == 0x08)
                {
                    // Speaker change is an implicit segment boundary
                    // (treat as FE×1 since it doesn't carry a multi-bubble
                    // hint).
                    Flush(1);
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
                    currentSegmentSpeaker = nameBuilder.ToString();
                    continue;
                }

                if (b == 0xFE)
                {
                    int feRun = 0;
                    while (i < bytes.Length && bytes[i] == 0xFE) { feRun++; i++; }
                    Flush(feRun);
                    continue;
                }

                if (b == 0xF8)
                {
                    // Both single and runs of 0xF8 are intra-bubble
                    // whitespace — the game's renderer reflows them visually.
                    while (i < bytes.Length && bytes[i] == 0xF8) i++;
                    text.Append('\n');
                    continue;
                }

                // Inline placeholders. 0xE0 = player name, 0xDA 0x68 = em-dash.
                if (b == 0xDA && i + 1 < bytes.Length && bytes[i + 1] == 0x68)
                {
                    text.Append(EmDashPlaceholder);
                    i += 2;
                    continue;
                }
                if (b == 0xE0)
                {
                    text.Append(PlayerNamePlaceholder);
                    i++;
                    continue;
                }

                if (b == 0xE2) { i += 2; continue; }
                if (b == 0xE3) { i += 2; continue; }

                var ch = DecodeByte(b);
                if (ch.HasValue) text.Append(ch.Value);
                i++;
            }
            Flush(1);

            // Second pass: paginate each segment into FeRun bubbles.
            var result = new List<DialogueBox>();
            foreach (var seg in segments)
            {
                if (seg.FeRun <= 1)
                {
                    result.Add(new DialogueBox(seg.Speaker, seg.Text));
                    continue;
                }
                foreach (var bubble in PaginateSegment(seg.Text, seg.FeRun))
                    result.Add(new DialogueBox(seg.Speaker, bubble));
            }
            return result;
        }

        /// <summary>
        /// Split a multi-bubble FE-segment into <paramref name="bubbleCount"/>
        /// bubbles by sentence boundaries. The TIC renderer balances
        /// character count across bubbles (target ≈ total/N per bubble),
        /// not sentence count. Verified at event 045 segments:
        ///   - "Might I pose…mire?" (5 sentences, FE×3) →
        ///     2+2+1 sentences with bubble char counts 127/114/90 ≈ 110.
        ///   - "It was not of your doing…position." (6 sentences, FE×4) →
        ///     2+1+2+1 sentences with bubble char counts 110/108/104/76 ≈ 99.
        /// We greedily pack sentences up to ~1.3× the average bubble budget
        /// while reserving at least one sentence for every remaining bubble.
        /// </summary>
        private static List<string> PaginateSegment(string text, int bubbleCount)
        {
            var sentences = SplitSentences(text);
            var bubbles = new List<string>(bubbleCount);
            if (sentences.Count == 0) return bubbles;
            // FE run longer than the sentence count: the trailing FEs
            // encode a pause beat, not extra empty bubbles (Dorter event 38).
            if (sentences.Count <= bubbleCount)
            {
                bubbles.AddRange(sentences);
                return bubbles;
            }

            int totalChars = 0;
            foreach (var s in sentences) totalChars += s.Length;
            double budget = (double)totalChars / bubbleCount;
            double softCap = budget * 1.3;

            int sentIdx = 0;
            for (int b = 0; b < bubbleCount; b++)
            {
                int bubblesLeft = bubbleCount - b - 1;
                int sentencesLeft = sentences.Count - sentIdx;
                // Reserve at least one sentence per remaining bubble.
                int maxTake = sentencesLeft - bubblesLeft;

                int take = 0;
                int chars = 0;
                while (take < maxTake)
                {
                    int next = chars + sentences[sentIdx + take].Length;
                    // Always take the first sentence of a bubble; only
                    // subsequent sentences are gated by the soft cap.
                    if (take == 0 || next <= softCap)
                    {
                        chars = next;
                        take++;
                    }
                    else
                    {
                        break;
                    }
                }

                // Last bubble absorbs any leftover sentences regardless
                // of cap so we never drop content.
                if (b == bubbleCount - 1)
                    take = sentencesLeft;

                var bubble = string.Join(" ", sentences.GetRange(sentIdx, take)).Trim();
                bubbles.Add(bubble);
                sentIdx += take;
            }
            return bubbles;
        }

        /// <summary>
        /// Split a passage into sentences. Sentences end at '.', '?' or '!'
        /// followed by whitespace or end of input. The terminal punctuation
        /// stays with the sentence. Internal whitespace (including newlines
        /// from 0xF8 reflow) is normalized to single spaces.
        /// </summary>
        internal static List<string> SplitSentences(string text)
        {
            var sentences = new List<string>();
            var current = new System.Text.StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n' || c == '\r') c = ' ';
                current.Append(c);
                bool isTerminal = c == '.' || c == '?' || c == '!';
                bool atEnd = i == text.Length - 1;
                bool followedByWs = !atEnd && (text[i + 1] == ' ' || text[i + 1] == '\n' || text[i + 1] == '\r');
                if (isTerminal && (atEnd || followedByWs))
                {
                    var s = System.Text.RegularExpressions.Regex.Replace(current.ToString(), @"\s+", " ").Trim();
                    if (s.Length > 0) sentences.Add(s);
                    current.Clear();
                }
            }
            if (current.Length > 0)
            {
                var s = System.Text.RegularExpressions.Regex.Replace(current.ToString(), @"\s+", " ").Trim();
                if (s.Length > 0) sentences.Add(s);
            }
            return sentences;
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

                // Inline placeholders. 0xE0 = player name, 0xDA 0x68 = em-dash.
                if (b == 0xDA && i + 1 < bytes.Length && bytes[i + 1] == 0x68)
                {
                    currentText.Append(EmDashPlaceholder);
                    sb.Append(EmDashPlaceholder);
                    i += 2;
                    continue;
                }
                if (b == 0xE0)
                {
                    currentText.Append(PlayerNamePlaceholder);
                    sb.Append(PlayerNamePlaceholder);
                    i++;
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
