using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace S1.Encryption;

/// <summary>Design D4: a database refuses to open with the wrong key or with no key, with a clear error.</summary>
public class RefusalTests(ITestOutputHelper output)
{
    [Fact]
    public void Wrong_key_is_refused()
    {
        using var temp = new TempFolder();
        var path = CreateDatabase(temp, out _);

        var ex = Assert.Throws<SqliteException>(() =>
        {
            using var connection = SpikeDb.Open(path, SpikeDb.RawKey(RandomNumberGenerator.GetBytes(32)));
            connection.Scalar("SELECT count(*) FROM t");
        });

        output.WriteLine($"wrong key: {ex.GetType().Name} SqliteErrorCode={ex.SqliteErrorCode} SqliteExtendedErrorCode={ex.SqliteExtendedErrorCode} Message=\"{ex.Message}\"");
        Assert.Equal(26, ex.SqliteErrorCode); // SQLITE_NOTADB
    }

    [Fact]
    public void Missing_key_is_refused()
    {
        using var temp = new TempFolder();
        var path = CreateDatabase(temp, out _);

        var ex = Assert.Throws<SqliteException>(() =>
        {
            using var connection = SpikeDb.Open(path, password: null);
            connection.Scalar("SELECT count(*) FROM t");
        });

        output.WriteLine($"no key: {ex.GetType().Name} SqliteErrorCode={ex.SqliteErrorCode} SqliteExtendedErrorCode={ex.SqliteExtendedErrorCode} Message=\"{ex.Message}\"");
        Assert.Equal(26, ex.SqliteErrorCode); // SQLITE_NOTADB
    }

    [Fact]
    public void Wrong_key_fails_at_Open_itself()
    {
        // Foundation needs to know where to catch: Microsoft.Data.Sqlite applies the key during Open().
        using var temp = new TempFolder();
        var path = CreateDatabase(temp, out _);

        using var connection = new SqliteConnection(SpikeDb.ConnectionString(path, SpikeDb.RawKey(RandomNumberGenerator.GetBytes(32))));
        var ex = Assert.Throws<SqliteException>(connection.Open);
        Assert.Equal(26, ex.SqliteErrorCode);
    }

    [Fact]
    public void Right_key_still_opens_after_refusals()
    {
        using var temp = new TempFolder();
        var path = CreateDatabase(temp, out var key);

        Assert.ThrowsAny<SqliteException>(() =>
        {
            using var c = SpikeDb.Open(path, SpikeDb.RawKey(RandomNumberGenerator.GetBytes(32)));
            c.Scalar("SELECT count(*) FROM t");
        });

        // A refused attempt must not damage the file.
        using var connection = SpikeDb.Open(path, SpikeDb.RawKey(key));
        Assert.Equal(1L, connection.Scalar("SELECT count(*) FROM t"));
    }

    private static string CreateDatabase(TempFolder temp, out byte[] key)
    {
        var path = temp.File("refusal.db");
        key = RandomNumberGenerator.GetBytes(32);
        using var connection = SpikeDb.Open(path, SpikeDb.RawKey(key));
        connection.Execute("CREATE TABLE t(x); INSERT INTO t VALUES (1);");
        return path;
    }
}
