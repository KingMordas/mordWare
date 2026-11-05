using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace S3.Storage.Probe;

/// <summary>
/// The spike's live database in LocalState\Data, with its two key blobs next to it:
/// spike.key (data key under DPAPI, daily use) and spike.recovery (data key under the recovery key,
/// copied into every backup header). Both are bound to the edition (S1).
/// </summary>
internal static class SpikeDatabase
{
    public const int SyntheticRows = 1000;

    public static string DatabasePath => Path.Combine(SpikeFolders.DataFolder, "spike.db");

    public static string DpapiKeyPath => Path.Combine(SpikeFolders.DataFolder, "spike.key");

    public static string RecoveryBlobPath => Path.Combine(SpikeFolders.DataFolder, "spike.recovery");

    /// <summary>A new encrypted WAL database with synthetic rows. Returns the recovery key (display form).</summary>
    public static string Create(SpikeEdition edition)
    {
        foreach (var file in new[] { DatabasePath, DatabasePath + "-wal", DatabasePath + "-shm", DpapiKeyPath, RecoveryBlobPath })
        {
            File.Delete(file);
        }

        var dataKey = KeyWrap.NewDataKey();
        var recoveryKey = RecoveryKey.Generate();
        try
        {
            using var connection = SpikeDb.Open(DatabasePath, SpikeDb.RawKey(dataKey));
            connection.Execute("PRAGMA journal_mode=WAL");
            connection.Execute("CREATE TABLE Items (Id INTEGER PRIMARY KEY, Label TEXT NOT NULL, Amount INTEGER NOT NULL)");
            using (var tx = connection.BeginTransaction())
            {
                using var insert = connection.CreateCommand();
                insert.Transaction = tx;
                insert.CommandText = "INSERT INTO Items (Label, Amount) VALUES ($label, $amount)";
                var label = insert.Parameters.Add("$label", SqliteType.Text);
                var amount = insert.Parameters.Add("$amount", SqliteType.Integer);
                for (var i = 1; i <= SyntheticRows; i++)
                {
                    label.Value = $"synthetic item {i}";
                    amount.Value = i * 12_345L;
                    insert.ExecuteNonQuery();
                }
                tx.Commit();
            }

            SaveKeys(dataKey, recoveryKey, edition);
            return RecoveryKey.Format(recoveryKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <summary>Writes both key blobs, bound to <paramref name="edition"/>.</summary>
    public static void SaveKeys(byte[] dataKey, string canonicalRecoveryKey, SpikeEdition edition)
    {
        File.WriteAllBytes(DpapiKeyPath, KeyWrap.ProtectWithDpapi(dataKey, edition));
        File.WriteAllBytes(RecoveryBlobPath, KeyWrap.WrapWithRecoveryKey(dataKey, canonicalRecoveryKey, edition));
    }

    /// <summary>The data key, unwrapped from spike.key with DPAPI under <paramref name="edition"/>.</summary>
    public static byte[] LoadDataKey(SpikeEdition edition) =>
        KeyWrap.UnprotectWithDpapi(File.ReadAllBytes(DpapiKeyPath), edition);

    /// <summary>Opens with the DPAPI key and counts rows; also proves a wrong key is refused (SQLite 26).</summary>
    public static void Open(SpikeEdition edition, Dictionary<string, object?> result)
    {
        var dataKey = LoadDataKey(edition);
        try
        {
            using var connection = SpikeDb.Open(DatabasePath, SpikeDb.RawKey(dataKey));
            result["rows"] = Convert.ToInt64(connection.Scalar("SELECT COUNT(*) FROM Items"));
            result["journalMode"] = connection.Scalar("PRAGMA journal_mode") as string;
            result["cipher"] = connection.Scalar("SELECT sqlite3mc_version()") as string;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }

        try
        {
            using var wrong = SpikeDb.Open(DatabasePath, SpikeDb.RawKey(KeyWrap.NewDataKey()));
            wrong.Scalar("SELECT COUNT(*) FROM Items");
            result["wrongKeyRefused"] = false;
        }
        catch (SqliteException ex)
        {
            result["wrongKeyRefused"] = ex.SqliteErrorCode == 26;
            result["wrongKeyError"] = $"SqliteException {ex.SqliteErrorCode}: {ex.Message}";
        }
    }
}
