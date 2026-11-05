using Microsoft.Data.Sqlite;

namespace S1.Encryption;

/// <summary>Design D2: the encrypted engine is the one that runs, and no second bundle is present.</summary>
public class EngineTests(ITestOutputHelper output)
{
    [Fact]
    public void Sqlite3mc_engine_is_loaded()
    {
        using var temp = new TempFolder();
        using var connection = SpikeDb.Open(temp.File("engine.db"), password: null);

        // sqlite3mc_version() exists only in SQLite3 Multiple Ciphers builds.
        var mcVersion = connection.Scalar("SELECT sqlite3mc_version()") as string;
        var sqliteVersion = connection.Scalar("SELECT sqlite_version()") as string;

        Assert.False(string.IsNullOrEmpty(mcVersion));
        output.WriteLine($"sqlite3mc_version(): {mcVersion}");
        output.WriteLine($"sqlite_version(): {sqliteVersion}");
    }

    [Fact]
    public void SQLitePCLRaw_core_is_version_3()
    {
        var core = typeof(SQLitePCL.raw).Assembly.GetName();
        var dataSqlite = typeof(SqliteConnection).Assembly.GetName();
        output.WriteLine($"{core.Name} {core.Version}");
        output.WriteLine($"{dataSqlite.Name} {dataSqlite.Version}");

        Assert.Equal(3, core.Version!.Major);
    }

    [Fact]
    public void Build_output_has_no_plain_e_sqlite3()
    {
        var natives = Directory.GetFiles(AppContext.BaseDirectory, "*.dll", SearchOption.AllDirectories)
            .Select(f => Path.GetFileName(f))
            .ToArray();

        Assert.DoesNotContain(natives, n => n.StartsWith("e_sqlite3", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(natives, n => n.Equals("SQLitePCLRaw.provider.e_sqlite3.dll", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(natives, n => n.Equals("sqlite3mc.dll", StringComparison.OrdinalIgnoreCase));
    }
}
