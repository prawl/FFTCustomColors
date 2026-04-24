using System;
using System.Collections.Generic;
using System.Text;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure helper for memory-hunt diffs — compares two byte snapshots of
    /// equal length and returns the offsets where they differ, plus a
    /// hex-string parser for the `blockData` response field (space-
    /// separated hex) and a human-readable formatter.
    ///
    /// Used by the `memory_diff` bridge action: caller passes the
    /// previous snapshot as a hex string in `pattern`, the bridge reads
    /// current memory at `address`/`blockSize`, diffs, and returns a
    /// formatted result. Enables memory hunts like "what byte changes
    /// between player turn and enemy turn?" without building diagnostic
    /// infrastructure per-hunt.
    /// </summary>
    public static class MemoryDiffCalculator
    {
        public readonly record struct ByteDiff(int Offset, byte Before, byte After);

        public static List<ByteDiff> Diff(byte[] before, byte[] after)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (after == null) throw new ArgumentNullException(nameof(after));
            if (before.Length != after.Length)
                throw new ArgumentException(
                    $"Snapshot lengths differ: before={before.Length} after={after.Length}",
                    nameof(after));

            var result = new List<ByteDiff>();
            for (int i = 0; i < before.Length; i++)
            {
                if (before[i] != after[i])
                    result.Add(new ByteDiff(i, before[i], after[i]));
            }
            return result;
        }

        /// <summary>
        /// Parse a hex string (tolerant of spaces) into a byte array.
        /// Empty / whitespace-only input returns an empty array.
        /// </summary>
        public static byte[] ParseHex(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return Array.Empty<byte>();
            var packed = hex.Replace(" ", "").Replace("\t", "").Replace("\n", "");
            if (packed.Length % 2 != 0)
                throw new ArgumentException(
                    $"Hex string has odd length ({packed.Length} chars)", nameof(hex));
            var bytes = new byte[packed.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(packed.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

        /// <summary>
        /// Format a diff list as newline-separated "0xNN: XX -> YY" lines,
        /// suitable for the bridge response BlockData field. Empty list
        /// returns "(no diffs)".
        /// </summary>
        public static string FormatDiffs(List<ByteDiff> diffs)
        {
            if (diffs == null || diffs.Count == 0) return "(no diffs)";
            var sb = new StringBuilder();
            for (int i = 0; i < diffs.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                var d = diffs[i];
                sb.Append($"0x{d.Offset:X2}: {d.Before:X2} -> {d.After:X2}");
            }
            return sb.ToString();
        }
    }
}
