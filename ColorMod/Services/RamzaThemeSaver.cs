using System;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// Saves Ramza theme data by converting SPR palette to NXD format.
    /// This service handles the conversion from edited BIN palette to charclut.nxd.
    /// </summary>
    public class RamzaThemeSaver
    {
        private readonly RamzaBinToNxdBridge _bridge;

        public RamzaThemeSaver()
        {
            _bridge = new RamzaBinToNxdBridge();
        }

        public RamzaThemeSaver(RamzaBinToNxdBridge bridge)
        {
            _bridge = bridge;
        }

        /// <summary>
        /// Checks if the job name represents a Ramza chapter.
        /// Accepts both short (RamzaCh1) and long (RamzaChapter1) formats.
        /// </summary>
        public bool IsRamzaChapter(string jobName)
        {
            return jobName == "RamzaCh1" || jobName == "RamzaCh23" || jobName == "RamzaCh4" ||
                   jobName == "RamzaChapter1" || jobName == "RamzaChapter23" || jobName == "RamzaChapter4";
        }

        /// <summary>
        /// Gets the chapter number from a Ramza job name.
        /// Accepts both short (RamzaCh1) and long (RamzaChapter1) formats.
        /// </summary>
        public int GetChapterFromJobName(string jobName)
        {
            return jobName switch
            {
                "RamzaCh1" or "RamzaChapter1" => 1,
                "RamzaCh23" or "RamzaChapter23" => 2,
                "RamzaCh4" or "RamzaChapter4" => 4,
                _ => throw new ArgumentException($"Job '{jobName}' is not a Ramza chapter")
            };
        }

        /// <summary>
        /// Normalizes Ramza job name to the canonical format (RamzaChapter1, RamzaChapter23, RamzaChapter4).
        /// This is used for consistent storage in UserThemes registry.
        /// </summary>
        public string NormalizeJobName(string jobName)
        {
            return jobName switch
            {
                "RamzaCh1" => "RamzaChapter1",
                "RamzaCh23" => "RamzaChapter23",
                "RamzaCh4" => "RamzaChapter4",
                _ => jobName // Already in canonical format or not a Ramza job
            };
        }

        /// <summary>
        /// Converts palette bytes (from SPR file) to CLUTData format.
        /// </summary>
        public int[] ConvertPaletteToClutData(byte[] paletteBytes)
        {
            return _bridge.ConvertBinPaletteToClutData(paletteBytes);
        }

        /// <summary>
        /// Creates a JSON array string from CLUTData.
        /// </summary>
        public string CreateClutDataJson(int[] clutData)
        {
            return JsonSerializer.Serialize(clutData);
        }

        /// <summary>
        /// Gets the deployment path for charclut.nxd.
        /// </summary>
        public string GetNxdDeploymentPath(string modPath)
        {
            return Path.Combine(modPath, "FFTIVC", "data", "enhanced", "nxd", "charclut.nxd");
        }

        /// <summary>
        /// Gets a temporary path for the SQLite database.
        /// </summary>
        public string GetSqliteTempPath()
        {
            return Path.Combine(Path.GetTempPath(), "charclut_fft.sqlite");
        }

        /// <summary>
        /// Gets the NXD Key and Key2 values for a chapter.
        /// Key maps: Ch1=1, Ch2/3=2, Ch4=3
        /// Key2 is always 0 for the base variant.
        /// </summary>
        public (int key, int key2) GetNxdKeyAndKey2(int chapter)
        {
            int key = chapter switch
            {
                1 => 1,
                2 => 2,
                4 => 3,
                _ => throw new ArgumentException($"Invalid chapter: {chapter}")
            };

            return (key, 0); // Key2=0 for base variant
        }

        /// <summary>
        /// Builds the SQL UPDATE statement to modify CLUTData.
        /// </summary>
        public string BuildUpdateSql(int key, int key2, string clutDataJson)
        {
            return $"UPDATE CharCLUT SET CLUTData = '{clutDataJson}' WHERE Key = {key} AND Key2 = {key2}";
        }

        /// <summary>
        /// Saves a Ramza theme by converting the palette to NXD format.
        /// This updates the SQLite database and patches the NXD file.
        /// </summary>
        /// <param name="jobName">Ramza job name (RamzaCh1, RamzaCh23, RamzaCh4)</param>
        /// <param name="paletteData">Palette bytes from the edited SPR file</param>
        /// <param name="modPath">Path to the mod directory</param>
        /// <returns>True if save was successful</returns>
        public bool SaveTheme(string jobName, byte[] paletteData, string modPath)
        {
            if (!IsRamzaChapter(jobName))
                throw new ArgumentException($"Job '{jobName}' is not a Ramza chapter");

            var chapter = GetChapterFromJobName(jobName);
            var clutData = ConvertPaletteToClutData(paletteData);
            var clutDataJson = CreateClutDataJson(clutData);

            // Get paths
            var baseSqlitePath = GetBaseSqlitePath();
            var workingSqlitePath = GetWorkingSqlitePath(modPath);
            var baseNxdPath = GetBaseNxdPath();
            var deployNxdPath = GetNxdDeploymentPath(modPath);

            // Ensure the working SQLite exists (copy from base if needed)
            EnsureWorkingSqliteExists(baseSqlitePath, workingSqlitePath);

            // Update the SQLite database
            if (!UpdateSqliteDatabase(workingSqlitePath, chapter, clutDataJson))
                return false;

            // Patch the NXD file from the updated SQLite
            var patcher = new NxdPatcher();
            return patcher.PatchNxdFromSqlite(baseNxdPath, workingSqlitePath, deployNxdPath);
        }

        /// <summary>
        /// Gets the path to the working SQLite database in the mod directory.
        /// </summary>
        public string GetWorkingSqlitePath(string modPath)
        {
            return Path.Combine(modPath, "Data", "nxd", "charclut.sqlite");
        }

        /// <summary>
        /// Gets the path to the base NXD template file.
        /// </summary>
        public string GetBaseNxdPath()
        {
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var assemblyDir = Path.GetDirectoryName(assemblyLocation) ?? "";
            return Path.Combine(assemblyDir, "Data", "nxd", "charclut.nxd");
        }

        /// <summary>
        /// Ensures the working SQLite database exists by copying from base if needed.
        /// </summary>
        private void EnsureWorkingSqliteExists(string baseSqlitePath, string workingSqlitePath)
        {
            if (File.Exists(workingSqlitePath))
                return;

            var workingDir = Path.GetDirectoryName(workingSqlitePath);
            if (!string.IsNullOrEmpty(workingDir) && !Directory.Exists(workingDir))
            {
                Directory.CreateDirectory(workingDir);
            }

            if (File.Exists(baseSqlitePath))
            {
                File.Copy(baseSqlitePath, workingSqlitePath);
            }
        }

        /// <summary>
        /// Gets the path to the base SQLite database bundled with the mod.
        /// </summary>
        public string GetBaseSqlitePath()
        {
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var assemblyDir = Path.GetDirectoryName(assemblyLocation) ?? "";
            return Path.Combine(assemblyDir, "Data", "nxd", "charclut.sqlite");
        }

        /// <summary>
        /// Updates the CLUTData in a SQLite database for a specific chapter.
        /// </summary>
        /// <param name="sqlitePath">Path to the SQLite database file</param>
        /// <param name="chapter">Chapter number (1, 2, or 4)</param>
        /// <param name="clutDataJson">JSON array of CLUTData values</param>
        /// <returns>True if update was successful</returns>
        public bool UpdateSqliteDatabase(string sqlitePath, int chapter, string clutDataJson)
        {
            var (key, key2) = GetNxdKeyAndKey2(chapter);

            try
            {
                using var connection = new SqliteConnection($"Data Source={sqlitePath}");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "UPDATE CharCLUT SET CLUTData = @clutData WHERE Key = @key AND Key2 = @key2";
                command.Parameters.AddWithValue("@clutData", clutDataJson);
                command.Parameters.AddWithValue("@key", key);
                command.Parameters.AddWithValue("@key2", key2);

                var rowsAffected = command.ExecuteNonQuery();
                connection.Close();
                return rowsAffected > 0;
            }
            finally
            {
                SqliteConnection.ClearAllPools();
            }
        }

        /// <summary>
        /// Reads CLUTData from a SQLite database for a specific key.
        /// </summary>
        /// <param name="sqlitePath">Path to the SQLite database file</param>
        /// <param name="key">Primary key value</param>
        /// <param name="key2">Secondary key value</param>
        /// <returns>JSON array string of CLUTData, or null if not found</returns>
        public string? ReadClutDataFromSqlite(string sqlitePath, int key, int key2)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={sqlitePath}");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT CLUTData FROM CharCLUT WHERE Key = @key AND Key2 = @key2";
                command.Parameters.AddWithValue("@key", key);
                command.Parameters.AddWithValue("@key2", key2);

                var result = command.ExecuteScalar();
                connection.Close();
                return result?.ToString();
            }
            finally
            {
                SqliteConnection.ClearAllPools();
            }
        }

        /// <summary>
        /// Applies a pre-computed CLUTData palette for a specific chapter.
        /// Used for built-in themes that have palettes computed from color transformations.
        /// </summary>
        /// <param name="chapter">Chapter number (1, 2, or 4)</param>
        /// <param name="clutData">Pre-computed CLUTData array (48 integers)</param>
        /// <param name="modPath">Path to the mod directory</param>
        /// <returns>True if the NXD was successfully patched</returns>
        public bool ApplyClutData(int chapter, int[] clutData, string modPath)
        {
            var clutDataJson = CreateClutDataJson(clutData);

            // Get paths
            var baseSqlitePath = GetBaseSqlitePath();
            var workingSqlitePath = GetWorkingSqlitePath(modPath);
            var baseNxdPath = GetBaseNxdPath();
            var deployNxdPath = GetNxdDeploymentPath(modPath);

            // Ensure the working SQLite exists (copy from base if needed)
            EnsureWorkingSqliteExists(baseSqlitePath, workingSqlitePath);

            // Update the SQLite database (UpdateSqliteDatabase handles connection pooling)
            if (!UpdateSqliteDatabase(workingSqlitePath, chapter, clutDataJson))
                return false;

            // Clear pools before NXD patching to ensure file is released
            SqliteConnection.ClearAllPools();

            // Patch the NXD file from the updated SQLite
            var patcher = new NxdPatcher();
            return patcher.PatchNxdFromSqlite(baseNxdPath, workingSqlitePath, deployNxdPath);
        }

        /// <summary>
        /// Applies CLUTData for all chapters at once.
        /// Used when applying a theme that affects all Ramza chapters.
        /// </summary>
        /// <param name="chapter1ClutData">CLUTData for Chapter 1 (or null to skip)</param>
        /// <param name="chapter23ClutData">CLUTData for Chapter 2/3 (or null to skip)</param>
        /// <param name="chapter4ClutData">CLUTData for Chapter 4 (or null to skip)</param>
        /// <param name="modPath">Path to the mod directory</param>
        /// <returns>True if all updates were successful</returns>
        public bool ApplyAllChaptersClutData(int[] chapter1ClutData, int[] chapter23ClutData, int[] chapter4ClutData, string modPath)
        {
            // Get paths
            var baseSqlitePath = GetBaseSqlitePath();
            var workingSqlitePath = GetWorkingSqlitePath(modPath);
            var baseNxdPath = GetBaseNxdPath();
            var deployNxdPath = GetNxdDeploymentPath(modPath);

            // Ensure the working SQLite exists (copy from base if needed)
            EnsureWorkingSqliteExists(baseSqlitePath, workingSqlitePath);

            // Update the SQLite database for each chapter using a single connection
            bool allUpdatesSuccessful = true;

            try
            {
                using var connection = new SqliteConnection($"Data Source={workingSqlitePath}");
                connection.Open();

                if (chapter1ClutData != null)
                {
                    var json = CreateClutDataJson(chapter1ClutData);
                    allUpdatesSuccessful &= UpdateSqliteDatabaseWithConnection(connection, 1, json);
                }

                if (chapter23ClutData != null)
                {
                    var json = CreateClutDataJson(chapter23ClutData);
                    allUpdatesSuccessful &= UpdateSqliteDatabaseWithConnection(connection, 2, json);
                }

                if (chapter4ClutData != null)
                {
                    var json = CreateClutDataJson(chapter4ClutData);
                    allUpdatesSuccessful &= UpdateSqliteDatabaseWithConnection(connection, 4, json);
                }

                connection.Close();
            }
            finally
            {
                // Clear the connection pool to release file handles
                SqliteConnection.ClearAllPools();
            }

            if (!allUpdatesSuccessful)
                return false;

            // Patch the NXD file from the updated SQLite
            var patcher = new NxdPatcher();
            return patcher.PatchNxdFromSqlite(baseNxdPath, workingSqlitePath, deployNxdPath);
        }

        /// <summary>
        /// Updates CLUTData using an existing connection (avoids connection pooling issues).
        /// </summary>
        private bool UpdateSqliteDatabaseWithConnection(SqliteConnection connection, int chapter, string clutDataJson)
        {
            var (key, key2) = GetNxdKeyAndKey2(chapter);

            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE CharCLUT SET CLUTData = @clutData WHERE Key = @key AND Key2 = @key2";
            command.Parameters.AddWithValue("@clutData", clutDataJson);
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@key2", key2);

            var rowsAffected = command.ExecuteNonQuery();
            return rowsAffected > 0;
        }

        /// <summary>
        /// Makes the EnsureWorkingSqliteExists method accessible for external use.
        /// </summary>
        public void EnsureWorkingSqlite(string modPath)
        {
            var baseSqlitePath = GetBaseSqlitePath();
            var workingSqlitePath = GetWorkingSqlitePath(modPath);
            EnsureWorkingSqliteExists(baseSqlitePath, workingSqlitePath);
        }
    }
}
