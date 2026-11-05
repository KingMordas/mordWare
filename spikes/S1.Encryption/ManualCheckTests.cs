using System.Security.Cryptography;

namespace S1.Encryption;

/// <summary>
/// Design D8, opt-in only: writes a synthetic sample so the owner can confirm a standard SQLCipher 4 tool
/// (DB Browser for SQLite) reads it. Run with:
///   dotnet test --project spikes/S1.Encryption --filter-class "*ManualCheck*" --explicit only --output Detailed
/// Delete %TEMP%\mordware-s1-manual afterwards. The key is throwaway and the data synthetic, so printing it is fine.
/// </summary>
public class ManualCheckTests(ITestOutputHelper output)
{
    [Fact(Explicit = true)]
    public void Write_sample_for_DB_Browser()
    {
        var folder = Path.Combine(Path.GetTempPath(), "mordware-s1-manual");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "sample.db");
        File.Delete(path);

        var key = RandomNumberGenerator.GetBytes(32);
        using (var connection = SpikeDb.Open(path, SpikeDb.RawKey(key)))
        {
            // Default rollback journal (not WAL), so the single file is complete for the external tool.
            connection.Execute("""
                CREATE TABLE refuel(id INTEGER PRIMARY KEY, day TEXT, odometer_km REAL, liters REAL, note TEXT);
                INSERT INTO refuel(day, odometer_km, liters, note) VALUES
                    ('2026-10-01', 12345.60, 41.250, 'synthetic sample row 1'),
                    ('2026-10-15', 12987.10, 38.900, 'synthetic sample row 2'),
                    ('2026-11-02', 13610.45, 40.125, 'synthetic sample row 3');
                """);
        }
        // Diagnostic twin with a passphrase (KDF path), to tell a raw-key problem from a cipher-format problem.
        var passPath = Path.Combine(folder, "sample-passphrase.db");
        File.Delete(passPath);
        using (var connection = SpikeDb.Open(passPath, "mordware-s1-check"))
        {
            connection.Execute("CREATE TABLE refuel(id INTEGER PRIMARY KEY, note TEXT); INSERT INTO refuel(note) VALUES ('synthetic passphrase row');");
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        output.WriteLine($"Passphrase twin: {passPath} (passphrase: mordware-s1-check)");

        output.WriteLine($"Sample: {path}");
        output.WriteLine("DB Browser for SQLite: Open Database -> SQLCipher 4 defaults -> key type 'Raw key' -> paste:");
        output.WriteLine("0x" + Convert.ToHexString(key));
        output.WriteLine("Expect table 'refuel' with 3 synthetic rows. Then delete the folder.");
    }
}
