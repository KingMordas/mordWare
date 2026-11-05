using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace S1.Encryption;

/// <summary>Design D7: SQLite's online backup API copies an encrypted database (WAL, live connection) safely.</summary>
public class BackupTests(ITestOutputHelper output)
{
    [Fact]
    public void Backup_to_a_destination_keyed_the_same_way_restores_and_opens()
    {
        using var temp = new TempFolder();
        var key = RandomNumberGenerator.GetBytes(32);
        var sourcePath = temp.File("live.db");
        var backupPath = temp.File("backup.db");

        using (var source = OpenFilledWalDatabase(sourcePath, key))
        {
            // The source stays open and has un-checkpointed WAL content, like the running app.
            Assert.True(new FileInfo(sourcePath + "-wal").Length > 0);

            using var destination = SpikeDb.Open(backupPath, SpikeDb.RawKey(key));
            source.BackupDatabase(destination);
        }

        using (var restored = SpikeDb.Open(backupPath, SpikeDb.RawKey(key)))
        {
            Assert.Equal(1000L, restored.Scalar("SELECT count(*) FROM t"));
            Assert.Equal("ok", restored.Scalar("PRAGMA integrity_check"));
        }

        Assert.Throws<SqliteException>(() => SpikeDb.Open(backupPath, password: null).Scalar("SELECT count(*) FROM t"));
        AtRestTests.AssertNoMarker(temp);
    }

    [Fact]
    public void Backup_to_a_destination_with_a_different_key_uses_the_destination_key()
    {
        using var temp = new TempFolder();
        var sourceKey = RandomNumberGenerator.GetBytes(32);
        var backupKey = RandomNumberGenerator.GetBytes(32);
        var backupPath = temp.File("backup-other-key.db");

        using (var source = OpenFilledWalDatabase(temp.File("live.db"), sourceKey))
        using (var destination = SpikeDb.Open(backupPath, SpikeDb.RawKey(backupKey)))
        {
            source.BackupDatabase(destination);
        }

        using (var restored = SpikeDb.Open(backupPath, SpikeDb.RawKey(backupKey)))
        {
            Assert.Equal(1000L, restored.Scalar("SELECT count(*) FROM t"));
        }
        Assert.Throws<SqliteException>(() => SpikeDb.Open(backupPath, SpikeDb.RawKey(sourceKey)).Scalar("SELECT count(*) FROM t"));
        AtRestTests.AssertNoMarker(temp);
    }

    [Fact]
    public void Backup_to_an_unkeyed_destination_is_refused_and_writes_no_plaintext()
    {
        // Observed 2026-11-04: SQLite3MC refuses with "backup is not supported with incompatible source and
        // target databases", so an encrypted database cannot be backed up into plaintext by mistake.
        using var temp = new TempFolder();
        var key = RandomNumberGenerator.GetBytes(32);
        var backupPath = temp.File("backup-unkeyed.db");

        Exception? error = null;
        using (var source = OpenFilledWalDatabase(temp.File("live.db"), key))
        using (var destination = SpikeDb.Open(backupPath, password: null))
        {
            try
            {
                source.BackupDatabase(destination);
            }
            catch (Exception ex)
            {
                error = ex;
            }
        }

        output.WriteLine($"unkeyed destination: {error?.GetType().Name}: {error?.Message}");
        var sqliteError = Assert.IsType<SqliteException>(error);
        Assert.Equal(1, sqliteError.SqliteErrorCode);
        Assert.False(HasMarker(temp), "no plaintext may reach the disk");
    }

    internal static SqliteConnection OpenFilledWalDatabase(string path, byte[] key)
    {
        var connection = SpikeDb.Open(path, SpikeDb.RawKey(key));
        connection.Execute("PRAGMA journal_mode=WAL;");
        connection.Execute($"CREATE TABLE t(x TEXT); WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < 1000) INSERT INTO t SELECT '{AtRestTests.Marker}' || i FROM n;");
        return connection;
    }

    private static bool HasMarker(TempFolder temp)
    {
        try
        {
            AtRestTests.AssertNoMarker(temp);
            return false;
        }
        catch (Xunit.Sdk.XunitException)
        {
            return true;
        }
    }
}
