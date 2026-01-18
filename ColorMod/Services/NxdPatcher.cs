using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Patches charclut.nxd files by updating CLUTData bytes in place.
    /// This avoids the need to fully reimplement the NXD format - we just
    /// read the SQLite, convert JSON to bytes, and patch the NXD template.
    /// </summary>
    public class NxdPatcher
    {
        // CLUTData offsets in charclut.nxd for each Key/Key2 combination
        // These were determined by analyzing the binary structure
        // Each CLUT is 48 bytes, stored consecutively starting at 0x379
        private static readonly Dictionary<(int key, int key2), int> ClutDataOffsets = new()
        {
            // Key 1 (Chapter 1) - 4 variants
            { (1, 0), 0x379 },
            { (1, 1), 0x3A9 },
            { (1, 2), 0x3D9 },
            { (1, 3), 0x409 },
            // Key 2 (Chapter 2/3) - 4 variants
            { (2, 0), 0x439 },
            { (2, 1), 0x469 },
            { (2, 2), 0x499 },
            { (2, 3), 0x4C9 },
            // Key 3 (Chapter 4) - 4 variants
            { (3, 0), 0x4F9 },
            { (3, 1), 0x529 },
            { (3, 2), 0x559 },
            { (3, 3), 0x589 },
            // Key 254 (special) - 2 variants
            { (254, 0), 0x5B9 },
            { (254, 1), 0x5E9 },
            // Key 255 (special) - 2 variants
            { (255, 0), 0x619 },
            { (255, 1), 0x649 },
        };

        private const int ClutDataSize = 48; // 16 colors × 3 RGB bytes

        /// <summary>
        /// Creates a patched NXD file from the SQLite database.
        /// </summary>
        /// <param name="templateNxdPath">Path to the template charclut.nxd</param>
        /// <param name="sqlitePath">Path to the modified charclut.sqlite</param>
        /// <param name="outputNxdPath">Path for the output NXD file</param>
        /// <returns>True if patching was successful</returns>
        public bool PatchNxdFromSqlite(string templateNxdPath, string sqlitePath, string outputNxdPath)
        {
            if (!File.Exists(templateNxdPath))
                throw new FileNotFoundException("Template NXD file not found", templateNxdPath);

            if (!File.Exists(sqlitePath))
                throw new FileNotFoundException("SQLite database not found", sqlitePath);

            // Read the template NXD
            byte[] nxdBytes = File.ReadAllBytes(templateNxdPath);

            // Verify it's a valid NXD file
            if (nxdBytes.Length < 4 ||
                nxdBytes[0] != 'N' || nxdBytes[1] != 'X' ||
                nxdBytes[2] != 'D' || nxdBytes[3] != 'F')
            {
                throw new InvalidDataException("Invalid NXD file - missing NXDF magic");
            }

            try
            {
                // Read CLUTData from SQLite and patch into NXD
                using var connection = new SqliteConnection($"Data Source={sqlitePath}");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT Key, Key2, CLUTData FROM CharCLUT";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    int key = reader.GetInt32(0);
                    int key2 = reader.GetInt32(1);
                    string clutDataJson = reader.GetString(2);

                    if (ClutDataOffsets.TryGetValue((key, key2), out int offset))
                    {
                        byte[] clutBytes = JsonToClutBytes(clutDataJson);
                        if (clutBytes.Length == ClutDataSize)
                        {
                            Array.Copy(clutBytes, 0, nxdBytes, offset, ClutDataSize);
                        }
                    }
                }

                connection.Close();
            }
            finally
            {
                // Clear connection pool to release file handles
                SqliteConnection.ClearAllPools();
            }

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputNxdPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Write the patched NXD
            File.WriteAllBytes(outputNxdPath, nxdBytes);

            return true;
        }

        /// <summary>
        /// Patches a single CLUTData entry in an NXD file.
        /// </summary>
        /// <param name="nxdPath">Path to the NXD file to patch</param>
        /// <param name="key">Primary key (1=Ch1, 2=Ch2/3, 3=Ch4)</param>
        /// <param name="key2">Secondary key (0=base variant)</param>
        /// <param name="clutDataJson">JSON array of 48 RGB values</param>
        public bool PatchSingleEntry(string nxdPath, int key, int key2, string clutDataJson)
        {
            if (!File.Exists(nxdPath))
                throw new FileNotFoundException("NXD file not found", nxdPath);

            if (!ClutDataOffsets.TryGetValue((key, key2), out int offset))
                throw new ArgumentException($"Unknown Key/Key2 combination: {key}/{key2}");

            byte[] clutBytes = JsonToClutBytes(clutDataJson);
            if (clutBytes.Length != ClutDataSize)
                throw new ArgumentException($"CLUTData must be exactly {ClutDataSize} bytes, got {clutBytes.Length}");

            // Read, patch, write
            byte[] nxdBytes = File.ReadAllBytes(nxdPath);
            Array.Copy(clutBytes, 0, nxdBytes, offset, ClutDataSize);
            File.WriteAllBytes(nxdPath, nxdBytes);

            return true;
        }

        /// <summary>
        /// Gets the offset for a specific Key/Key2 combination.
        /// </summary>
        public int? GetClutDataOffset(int key, int key2)
        {
            return ClutDataOffsets.TryGetValue((key, key2), out int offset) ? offset : null;
        }

        /// <summary>
        /// Converts a JSON array of integers to raw bytes.
        /// </summary>
        private byte[] JsonToClutBytes(string json)
        {
            var values = JsonSerializer.Deserialize<int[]>(json);
            if (values == null)
                return Array.Empty<byte>();

            byte[] bytes = new byte[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                bytes[i] = (byte)Math.Clamp(values[i], 0, 255);
            }
            return bytes;
        }

        /// <summary>
        /// Converts raw bytes to a JSON array of integers.
        /// </summary>
        public string ClutBytesToJson(byte[] bytes)
        {
            int[] values = new int[bytes.Length];
            for (int i = 0; i < bytes.Length; i++)
            {
                values[i] = bytes[i];
            }
            return JsonSerializer.Serialize(values);
        }
    }
}
