using System;
using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Looks up a unit's job/monster class name from an 11-byte fingerprint
    /// read from the heap unit struct at offset +0x69.
    ///
    /// The FFT IC remaster keeps enemy data in heap-allocated unit structs.
    /// The UI buffer for "current hovered unit" is unreliable during C+Up scan,
    /// and the condensed struct has no jobId byte. However, bytes at struct+0x69
    /// through struct+0x73 are a class fingerprint — identical across instances
    /// of the same class, distinct between classes.
    ///
    /// Empirically determined: byte 0 of the fingerprint (struct+0x69) is
    /// per-unit/team variation — two Knights (one player, one enemy) had bytes
    /// 02-0A-78-0F-... and 03-0A-78-0F-... respectively, differing only at byte 0.
    /// The true class signature is bytes 1-10 (10 bytes from struct+0x6A to +0x73).
    /// Stored keys therefore skip byte 0 to support both teams.
    ///
    /// This is our most reliable enemy class identifier. Player units should
    /// still prefer the roster lookup (which is authoritative); this is the
    /// fallback for any unit that can't be matched against the roster.
    ///
    /// See memory/project_class_fingerprint.md for the investigation history.
    /// </summary>
    public static class ClassFingerprintLookup
    {
        // Keys are bytes 1-10 of the 11-byte fingerprint (10 bytes total).
        // Byte 0 is skipped because it varies per-unit/team for the same class.
        //
        // Seeded from two Siedge Weald random encounters (2026-04-10) with
        // user-provided ground truth.
        private static readonly Dictionary<string, string> FingerprintToJob = new()
        {
            // From Siedge Weald encounter 1:
            ["05-65-1E-03-55-66-27-7D-07-58"] = "Skeletal Fiend",
            ["06-56-1E-23-55-72-27-67-07-57"] = "Black Goblin",
            ["07-57-1E-1E-5A-73-27-55-07-5E"] = "Grenade",
            ["0C-4B-09-78-64-64-3C-3C-32-96"] = "Black Mage",
            ["09-87-0D-50-64-6E-30-81-32-50"] = "Monk",

            // From Siedge Weald encounter 2:
            ["0D-46-08-7D-64-5A-46-32-32-7D"] = "Summoner",
            ["0C-50-10-4B-64-64-4B-4B-32-50"] = "Chemist",
            ["0B-64-10-41-64-64-2D-6E-32-50"] = "Archer",
            ["0C-4B-0A-6E-64-64-3C-32-32-78"] = "Mystic",
            ["0A-78-0F-50-64-64-28-78-32-50"] = "Knight",
            // Note: Ramza (Gallant Knight) fingerprint differs between saves
            // (saw "0B-78-0B-6E-5F-64-32-73-30-73" and "0C-50-14-5A-64-64-28-8C-32-50").
            // Likely encodes equipment/stat growth for story chars. Use roster lookup
            // for Ramza instead.
        };

        /// <summary>
        /// Look up a job name from an 11-byte fingerprint. Returns null if unknown.
        /// Uses bytes 1-10 as the key (byte 0 is team/unit variation).
        /// </summary>
        public static string? GetJobName(byte[] fingerprint)
        {
            if (fingerprint == null || fingerprint.Length < 11) return null;
            var key = BitConverter.ToString(fingerprint, 1, 10);
            return FingerprintToJob.TryGetValue(key, out var name) ? name : null;
        }

        /// <summary>
        /// Convert an 11-byte fingerprint to its canonical string key (for logging).
        /// Returns the full 11-byte hex for diagnostics, with byte 0 separated.
        /// </summary>
        public static string ToKey(byte[] fingerprint)
        {
            if (fingerprint == null || fingerprint.Length < 11) return "";
            return BitConverter.ToString(fingerprint, 0, 11);
        }
    }
}
