using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace S1.Encryption;

/// <summary>
/// Design D8: timings for the owner's decision. Guideline: encrypted open under ~50 ms, encryption overhead
/// under 2x. Numbers outside it are reported, not failed; only the raw-key-skips-the-KDF claim is asserted.
/// Prints timings only, never a key.
/// </summary>
public class MeasureTests(ITestOutputHelper output)
{
    private const int Opens = 20;
    private const int Rows = 10_000;

    [Fact]
    public void Open_time_raw_key_vs_passphrase()
    {
        using var temp = new TempFolder();
        var rawPath = temp.File("raw.db");
        var passPath = temp.File("pass.db");
        var rawKey = SpikeDb.RawKey(RandomNumberGenerator.GetBytes(32));
        var passphrase = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); // same entropy, but run through the KDF

        Create(rawPath, rawKey);
        Create(passPath, passphrase);
        OpenAndQuery(rawPath, rawKey); // warm the native library once
        OpenAndQuery(passPath, passphrase);

        var raw = MedianMs(() => OpenAndQuery(rawPath, rawKey));
        var pass = MedianMs(() => OpenAndQuery(passPath, passphrase));

        output.WriteLine($"median open + first query over {Opens} opens: raw key {raw:F2} ms, passphrase (SQLCipher v4 KDF) {pass:F2} ms");
        Assert.True(raw < pass, "the raw key must skip the KDF and open faster than a passphrase");
    }

    [Fact]
    public void Insert_and_read_encrypted_vs_unkeyed()
    {
        using var temp = new TempFolder();
        var rawKey = SpikeDb.RawKey(RandomNumberGenerator.GetBytes(32));

        // Best of 3 alternating rounds, so neither side pays the cold start alone.
        double plainInsert = double.MaxValue, plainRead = double.MaxValue, encInsert = double.MaxValue, encRead = double.MaxValue;
        for (var round = 0; round < 3; round++)
        {
            var (pi, pr) = InsertAndRead(temp.File($"plain{round}.db"), password: null);
            var (ei, er) = InsertAndRead(temp.File($"enc{round}.db"), rawKey);
            (plainInsert, plainRead) = (Math.Min(plainInsert, pi), Math.Min(plainRead, pr));
            (encInsert, encRead) = (Math.Min(encInsert, ei), Math.Min(encRead, er));
        }

        output.WriteLine($"{Rows} rows, one transaction, WAL, best of 3: insert unkeyed {plainInsert:F1} ms / encrypted {encInsert:F1} ms (x{encInsert / plainInsert:F2})");
        output.WriteLine($"{Rows} rows read back (fresh connection): unkeyed {plainRead:F1} ms / encrypted {encRead:F1} ms (x{encRead / plainRead:F2})");
    }

    private static void Create(string path, string password)
    {
        using var connection = SpikeDb.Open(path, password);
        connection.Execute("CREATE TABLE t(x); INSERT INTO t VALUES (1);");
    }

    private static void OpenAndQuery(string path, string password)
    {
        using var connection = SpikeDb.Open(path, password);
        connection.Scalar("SELECT count(*) FROM t");
    }

    private static double MedianMs(Action action)
    {
        var samples = new double[Opens];
        for (var i = 0; i < Opens; i++)
        {
            var sw = Stopwatch.StartNew();
            action();
            samples[i] = sw.Elapsed.TotalMilliseconds;
        }
        Array.Sort(samples);
        return (samples[Opens / 2 - 1] + samples[Opens / 2]) / 2;
    }

    private static (double InsertMs, double ReadMs) InsertAndRead(string path, string? password)
    {
        double insertMs;
        using (var connection = SpikeDb.Open(path, password))
        {
            connection.Execute("PRAGMA journal_mode=WAL; CREATE TABLE refuel(id INTEGER PRIMARY KEY, odometer INTEGER, liters INTEGER, amount INTEGER, currency TEXT, note TEXT);");

            var sw = Stopwatch.StartNew();
            using var transaction = connection.BeginTransaction();
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO refuel(odometer, liters, amount, currency, note) VALUES ($o, $l, $a, 'EUR', $n)";
            var o = insert.Parameters.Add("$o", SqliteType.Integer);
            var l = insert.Parameters.Add("$l", SqliteType.Integer);
            var a = insert.Parameters.Add("$a", SqliteType.Integer);
            var n = insert.Parameters.Add("$n", SqliteType.Text);
            for (var i = 0; i < Rows; i++)
            {
                o.Value = 1_000_000L + i * 50_000L;
                l.Value = 40_000L + i % 7;
                a.Value = 700_000L + i;
                n.Value = "synthetic refuel " + i;
                insert.ExecuteNonQuery();
            }
            transaction.Commit();
            insertMs = sw.Elapsed.TotalMilliseconds;
        }

        using (var connection = SpikeDb.Open(path, password))
        {
            var sw = Stopwatch.StartNew();
            using var read = connection.CreateCommand();
            read.CommandText = "SELECT id, odometer, liters, amount, currency, note FROM refuel ORDER BY odometer";
            using var reader = read.ExecuteReader();
            var count = 0;
            while (reader.Read())
            {
                count++;
            }
            var readMs = sw.Elapsed.TotalMilliseconds;
            Assert.Equal(Rows, count);
            return (insertMs, readMs);
        }
    }
}
