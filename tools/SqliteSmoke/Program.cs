// SQLite functional smoke for the release zip (CC-10).
//
// Extracts the packaged zip to a temp dir and opens a real SqliteConnection using the
// DLLs exactly as shipped. This reproduces the user-facing failure mode: SQLitePCLRaw
// pinvokes e_sqlite3, which resolves from the provider assembly's own directory, so a
// zip missing the root-level e_sqlite3.dll fails here with the same
// "type initializer for SqliteConnection" error users reported. Exit 0 = pass, 1 = fail.
//
// Usage: dotnet run --project tools/SqliteSmoke -c Release -- <path-to-release-zip>

using System.IO.Compression;
using System.Reflection;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: SqliteSmoke <release-zip>");
    return 1;
}

string tempDir = Path.Combine(Path.GetTempPath(), "cc_sqlite_smoke_" + Guid.NewGuid().ToString("N"));
try
{
    ZipFile.ExtractToDirectory(args[0], tempDir);

    string sqliteDll = Directory
        .GetFiles(tempDir, "Microsoft.Data.Sqlite.dll", SearchOption.AllDirectories)
        .FirstOrDefault();
    if (sqliteDll is null)
    {
        Console.Error.WriteLine("SMOKE FAIL: Microsoft.Data.Sqlite.dll not found in the zip.");
        return 1;
    }

    var asm = Assembly.LoadFrom(sqliteDll);
    var connType = asm.GetType("Microsoft.Data.Sqlite.SqliteConnection", throwOnError: true);

    using var conn = (IDisposable)Activator.CreateInstance(connType, "Data Source=:memory:");
    connType.GetMethod("Open").Invoke(conn, null);

    dynamic dconn = conn;
    var cmd = dconn.CreateCommand();
    cmd.CommandText = "SELECT 1";
    object result = cmd.ExecuteScalar();

    Console.WriteLine($"SMOKE PASS: SqliteConnection opened from packaged layout (SELECT 1 = {result}).");
    return 0;
}
catch (Exception ex)
{
    var root = ex;
    while (root.InnerException is not null) root = root.InnerException;
    Console.Error.WriteLine($"SMOKE FAIL: {ex.GetType().Name}: {ex.Message}");
    Console.Error.WriteLine($"  root cause: {root.GetType().Name}: {root.Message}");
    return 1;
}
finally
{
    try { Directory.Delete(tempDir, recursive: true); } catch { }
}
