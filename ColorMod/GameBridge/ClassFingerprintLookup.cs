using System;
using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Looks up a unit's job/monster class name from an 11-byte fingerprint
    /// read from the heap unit struct at offset +0x69.
    ///
    /// The FFT IC remaster keeps enemy data in heap-allocated unit structs
    /// (0x4166Cxxxx..0x41671Exxxx range, 0x200-aligned slots). The UI buffer
    /// for "current hovered unit" is unreliable during C+Up scan, and the
    /// condensed struct has no jobId byte. However, bytes at struct+0x69
    /// through struct+0x73 are a deterministic class fingerprint — identical
    /// across instances of the same class, distinct between classes.
    ///
    /// This is our most reliable enemy class identifier. Player units should
    /// still prefer the roster lookup (which is authoritative); this is the
    /// fallback for any unit that can't be matched against the roster.
    ///
    /// See memory/project_class_fingerprint.md for the investigation history.
    /// </summary>
    public static class ClassFingerprintLookup
    {
        // Fingerprints stored as hex strings for readability. 11 bytes each.
        // Seeded from Siedge Weald random encounter (2026-04-10) with user-provided
        // ground truth for each enemy.
        private static readonly Dictionary<string, string> FingerprintToJob = new()
        {
            ["0F-05-65-1E-03-55-66-27-7D-07-58"] = "Skeletal Fiend",
            ["10-06-56-1E-23-55-72-27-67-07-57"] = "Black Goblin",
            ["10-07-57-1E-1E-5A-73-27-55-07-5E"] = "Grenade",
            ["03-0C-4B-09-78-64-64-3C-3C-32-96"] = "Black Mage",
            ["02-09-87-0D-50-64-6E-30-81-32-50"] = "Monk",
            ["03-0B-78-0B-6E-5F-64-32-73-30-73"] = "Gallant Knight",
        };

        /// <summary>
        /// Look up a job name from an 11-byte fingerprint. Returns null if unknown.
        /// </summary>
        public static string? GetJobName(byte[] fingerprint)
        {
            if (fingerprint == null || fingerprint.Length < 11) return null;
            var key = BitConverter.ToString(fingerprint, 0, 11);
            return FingerprintToJob.TryGetValue(key, out var name) ? name : null;
        }

        /// <summary>
        /// Convert an 11-byte fingerprint to its canonical string key (for logging).
        /// </summary>
        public static string ToKey(byte[] fingerprint)
        {
            if (fingerprint == null || fingerprint.Length < 11) return "";
            return BitConverter.ToString(fingerprint, 0, 11);
        }
    }
}
