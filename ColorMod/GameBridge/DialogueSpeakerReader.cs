using System;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Resolves the active dialog speaker name by following an 8-byte u64
    /// pointer into the engine's speaker-name string table.
    ///
    /// <para>The IC remaster keeps a per-box speaker name in heap as a
    /// pointer (the active dialog widget's "current speaker" field). The
    /// pointer targets a null-terminated ASCII string in a module-data
    /// string table at roughly <c>0x4E17600000 - 0x4E18000000</c>. The
    /// .mes file's <c>0xE3 0x08</c> tags only mark scene-opener speakers
    /// and miss every mid-scene rotation; reading this pointer fills the
    /// gap.</para>
    ///
    /// <para>See <c>memory/project_dialogue_speaker_pointer.md</c> for the
    /// hunt that surfaced this — verified Well-dressed Man / Dycedarg /
    /// Duke Larg in event 045.</para>
    ///
    /// <para>Pure helper: takes a read-bytes callback so it's testable
    /// without live memory.</para>
    /// </summary>
    public class DialogueSpeakerReader
    {
        // Bound the acceptable pointer range. The string-table address
        // varies per session (module data load is non-deterministic), so
        // we use a broad user-mode-canonical window. A pointer below
        // 256MB is a small int / handle, not a real string address;
        // anything above 47-bit canonical is non-canonical on x64. The
        // ASCII-validation step in DecodeNullTerminatedAscii catches
        // false-positive pointers that happen to land in this window.
        private const long StringTableMin = 0x10000000L;        // 256 MB
        private const long StringTableMax = 0x800000000000L;    // 47-bit canonical user-mode max

        // How many bytes to read at the resolved string address. The
        // observed table entries are short (single-word names up to
        // ~24 chars like "Adrammelech, the Wroth"); 64 is a safe cap.
        private const int MaxStringBytes = 64;

        private readonly Func<long, byte[]?> _readBytes;

        public DialogueSpeakerReader(Func<long, byte[]?> readBytes)
        {
            _readBytes = readBytes ?? throw new ArgumentNullException(nameof(readBytes));
        }

        /// <summary>
        /// Resolve the speaker name from an 8-byte LE pointer snapshot.
        /// Returns the null-terminated ASCII string the pointer targets,
        /// or <c>null</c> when the pointer is invalid (zero, out of the
        /// expected string-table range, or points to non-ASCII bytes).
        /// </summary>
        public string? Read(byte[] pointerBytes)
        {
            if (pointerBytes == null || pointerBytes.Length < 8) return null;

            long addr = ReadU64Le(pointerBytes);
            if (addr < StringTableMin || addr >= StringTableMax) return null;

            var stringBytes = _readBytes(addr);
            if (stringBytes == null || stringBytes.Length == 0) return null;

            return DecodeNullTerminatedAscii(stringBytes);
        }

        private static long ReadU64Le(byte[] bytes)
        {
            return ((long)bytes[0])
                 | ((long)bytes[1] << 8)
                 | ((long)bytes[2] << 16)
                 | ((long)bytes[3] << 24)
                 | ((long)bytes[4] << 32)
                 | ((long)bytes[5] << 40)
                 | ((long)bytes[6] << 48)
                 | ((long)bytes[7] << 56);
        }

        private static string? DecodeNullTerminatedAscii(byte[] bytes)
        {
            int len = 0;
            while (len < bytes.Length && len < MaxStringBytes && bytes[len] != 0)
            {
                byte b = bytes[len];
                // Speaker names are plain printable ASCII (letters, space,
                // punctuation, comma). Reject anything else as corrupted.
                if (b < 0x20 || b > 0x7E) return null;
                len++;
            }
            if (len == 0) return null;
            return System.Text.Encoding.ASCII.GetString(bytes, 0, len);
        }
    }
}
