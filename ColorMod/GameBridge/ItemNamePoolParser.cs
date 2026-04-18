using System;
using System.Collections.Generic;
using System.Text;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure parser for the static item-name pool at game address ~0x3F18000.
    /// Record layout per memory/project_item_name_pool.md (session 16 investigation):
    ///
    ///   offset  size  meaning
    ///   0x00    8     pointer (shared/locale, not useful for lookup)
    ///   0x08    4     length (u32, in UTF-16 chars)
    ///   0x0C    N*2   UTF-16LE name bytes
    ///   ...           zero-padding to 4-byte boundary before next record
    ///
    /// Inputs are raw bytes read from memory. Parser walks records until it
    /// hits a zero-length entry (sentinel) or an implausibly large length
    /// (corruption / past-end), then stops.
    ///
    /// Used by downstream resolvers that map hovered-item-index to display
    /// name. The live hovered-item-ID itself is a separate UE4 widget lookup
    /// that isn't solved yet (see project_item_name_pool.md) — this parser
    /// is the name-resolution half of the eventual pipeline.
    /// </summary>
    public static class ItemNamePoolParser
    {
        private const int HeaderSize = 12; // 8 pointer + 4 length
        private const int MaxReasonableLength = 64; // item names are &lt; 64 chars
        private const int RecordAlign = 4;

        public class NamePoolRecord
        {
            public string Name { get; set; } = "";
            public int CharLength { get; set; }
            /// <summary>Offset of the record's start inside the input buffer.</summary>
            public int OffsetInBuffer { get; set; }
        }

        /// <summary>
        /// Walk the buffer and decode every record until a stop condition
        /// (zero-length sentinel, implausible length, or buffer exhausted).
        /// </summary>
        public static List<NamePoolRecord> Decode(byte[] bytes)
        {
            var result = new List<NamePoolRecord>();
            if (bytes == null || bytes.Length < HeaderSize) return result;

            int cursor = 0;
            while (cursor + HeaderSize <= bytes.Length)
            {
                int length = ReadInt32LE(bytes, cursor + 8);
                if (length <= 0) break;                         // sentinel
                if (length > MaxReasonableLength) break;        // corrupted
                int nameBytes = length * 2;
                if (cursor + HeaderSize + nameBytes > bytes.Length) break;

                var name = Encoding.Unicode.GetString(bytes, cursor + HeaderSize, nameBytes);
                result.Add(new NamePoolRecord
                {
                    Name = name,
                    CharLength = length,
                    OffsetInBuffer = cursor,
                });

                // Advance cursor past header + body + padding to next record.
                int consumed = HeaderSize + nameBytes;
                int padded = ((consumed + RecordAlign - 1) / RecordAlign) * RecordAlign;
                cursor += padded;
            }
            return result;
        }

        public static NamePoolRecord? GetByIndex(List<NamePoolRecord> records, int index)
        {
            if (records == null) return null;
            if (index < 0 || index >= records.Count) return null;
            return records[index];
        }

        public static NamePoolRecord? FindByName(List<NamePoolRecord> records, string? name)
        {
            if (records == null || string.IsNullOrEmpty(name)) return null;
            foreach (var r in records)
            {
                if (string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
                    return r;
            }
            return null;
        }

        private static int ReadInt32LE(byte[] buf, int offset)
        {
            return buf[offset]
                 | (buf[offset + 1] << 8)
                 | (buf[offset + 2] << 16)
                 | (buf[offset + 3] << 24);
        }
    }
}
