using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Decodes world_wldmes_bin.en.bin — the packed source file for tavern rumor/errand
    /// body text and related world-map prose. Uses the same PSX byte→char mapping as
    /// MesDecoder but with extra bytes specific to this file (e.g. 0x95 as space, 0xDA
    /// and 0xD1 digraph prefixes).
    /// </summary>
    public static class WorldMesDecoder
    {
        /// <summary>
        /// Decode a single byte. Superset of MesDecoder.DecodeByte for bytes observed
        /// in world_wldmes_bin.en.bin.
        /// </summary>
        public static char? DecodeByte(byte b)
        {
            if (b >= 0x00 && b <= 0x09) return (char)('0' + b);
            if (b >= 0x0A && b <= 0x23) return (char)('A' + b - 0x0A);
            if (b >= 0x24 && b <= 0x3D) return (char)('a' + b - 0x24);
            if (b == 0x88 || b == 0xFA || b == 0x95) return ' ';
            if (b == 0x78 || b == 0x5F || b == 0x8E) return '.';
            if (b == 0x3E) return '!';
            if (b == 0x40 || b == 0x7B) return '?';
            if (b == 0x46) return ':';
            if (b == 0x8B || b == 0x8D) return ',';
            if (b == 0x3F || b == 0x7D || b == 0x93) return '\'';
            if (b == 0x45) return ';';
            if (b == 0x47 || b == 0x91) return '"';
            if (b == 0x7C) return '\n';
            if (b == 0x44) return '-';
            if (b == 0x48) return '(';
            if (b == 0x49) return ')';
            if (b == 0xB7) return '/';
            return null;
        }

        /// <summary>
        /// Two-byte digraph expansions observed in world_wldmes_bin.en.bin.
        /// Key is (hi, lo), value is the string that replaces the pair.
        /// Populated iteratively as unknown pairs appear in debug dumps.
        /// </summary>
        private static readonly Dictionary<(byte, byte), string> Digraphs = new()
        {
            // DA 74 = "," — paired comma sequence (the following 0x95 renders the space).
            { (0xDA, 0x74), "," },
            // D1 1D = "-" in compound words (high-ranking, half-century, road-building).
            // 0x1D normally decodes to 'T' but D1-prefixed it's a dash marker.
            { (0xD1, 0x1D), "-" },
        };

        /// <summary>
        /// Decode bytes to readable text. Unknown bytes are rendered as &lt;HH&gt; so
        /// debug dumps surface them for iteration.
        /// </summary>
        public static string Decode(byte[] bytes, int offset, int length, bool markUnknown = true)
        {
            var sb = new StringBuilder(length);
            int end = offset + length;
            for (int i = offset; i < end; i++)
            {
                byte b = bytes[i];

                // Brave-Story date-stamp glyph: F5 66 F6 XX F5 YY F6 ZZ (8 bytes).
                // Renders as a fancy date stamp at the top of the rumor card. Skip entirely.
                if (b == 0xF5 && i + 7 < end
                    && bytes[i + 1] == 0x66
                    && bytes[i + 2] == 0xF6
                    && bytes[i + 4] == 0xF5
                    && bytes[i + 6] == 0xF6)
                {
                    i += 7;
                    continue;
                }

                // Two-byte digraph
                if (i + 1 < end && Digraphs.TryGetValue((b, bytes[i + 1]), out var digraph))
                {
                    sb.Append(digraph);
                    i++;
                    continue;
                }

                // E3 + following byte = structural marker (title bracket, speaker tag etc.)
                // Skip the pair in raw-text decode; record-splitting happens above.
                if (b == 0xE3 && i + 1 < end)
                {
                    i++;
                    continue;
                }

                // F8 = paragraph break within a record → render as space.
                if (b == 0xF8) { sb.Append(' '); continue; }

                // FE = harder break (often section/title separator) → render as newline.
                if (b == 0xFE) { sb.Append('\n'); continue; }

                var c = DecodeByte(b);
                if (c.HasValue) sb.Append(c.Value);
                else if (markUnknown) sb.Append('<').Append(b.ToString("x2")).Append('>');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Length of actual content in the file (trims trailing zero padding).
        /// </summary>
        public static int ContentLength(byte[] bytes)
        {
            int i = bytes.Length - 1;
            while (i >= 0 && bytes[i] == 0) i--;
            return i + 1;
        }

        public record Rumor(int Index, int Offset, string Body);

        /// <summary>
        /// Extract rumor/Brave-Story body records from the pre-title region of the file
        /// (before the first 0xE3 0x08 Chronicle-title marker). Records are FE-separated.
        /// Leading date-stamp glyphs (e.g. F5 66 F6 XX F5 X F6 1) are stripped from body
        /// text since they render as byte-escapes.
        /// </summary>
        public static List<Rumor> ExtractRumors(byte[] bytes)
        {
            var result = new List<Rumor>();
            int contentLen = ContentLength(bytes);

            int firstTitle = contentLen;
            for (int i = 0; i + 1 < contentLen; i++)
            {
                if (bytes[i] == 0xE3 && bytes[i + 1] == 0x08) { firstTitle = i; break; }
            }

            // Record split strategy: in the pre-title region, split on either
            //   (a) 0xFE (section break), OR
            //   (b) the start of an 8-byte date-stamp glyph F5 66 F6 XX F5 YY F6 ZZ
            // whichever comes first. This captures chapter-1 Brave-Story rumors (which
            // lack the date-stamp prefix, FE-bounded) AND chapter-2+ rumors (each
            // preceded by its own date-stamp glyph, sometimes multiple stacked).
            int chunkStart = 0;
            int idx = 0;
            for (int i = 0; i <= firstTitle; i++)
            {
                bool isGlyphStart = (i + 7 < firstTitle
                    && bytes[i] == 0xF5 && bytes[i + 1] == 0x66 && bytes[i + 2] == 0xF6
                    && bytes[i + 4] == 0xF5 && bytes[i + 6] == 0xF6);
                bool isFE = (i < contentLen && bytes[i] == 0xFE);
                bool isEnd = (i == firstTitle);

                if (isGlyphStart || isFE || isEnd)
                {
                    if (i > chunkStart)
                    {
                        var body = Decode(bytes, chunkStart, i - chunkStart, markUnknown: false).Trim();
                        body = StripDateGlyphs(body);
                        if (body.Length > 0)
                        {
                            result.Add(new Rumor(idx, chunkStart, body));
                            idx++;
                        }
                    }
                    // Advance past the boundary: 1 byte for FE, 8 bytes for glyph, 0 for end.
                    if (isFE) chunkStart = i + 1;
                    else if (isGlyphStart) { chunkStart = i; i += 7; }
                    // isEnd: loop terminates
                }
            }
            return result;
        }

        /// <summary>
        /// Remove leading F5/F6 byte-escape sequences that survive Decode(markUnknown:false)
        /// as empty text. Safety net in case future markUnknown toggles change.
        /// </summary>
        private static string StripDateGlyphs(string s) =>
            s.TrimStart(' ', '\n', '\r', '\t');
    }
}
