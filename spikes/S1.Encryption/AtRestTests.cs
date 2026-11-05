using System.Security.Cryptography;
using System.Text;

namespace S1.Encryption;

/// <summary>Design D3: in WAL mode, no plaintext reaches the database, WAL or shared-memory files.</summary>
public class AtRestTests
{
    internal const string Marker = "MORDWARE-PLAINTEXT-MARKER-7f3a";

    [Fact]
    public void Marker_is_absent_from_every_file_before_and_after_checkpoint()
    {
        using var temp = new TempFolder();
        var path = temp.File("atrest.db");
        var key = RandomNumberGenerator.GetBytes(32);

        using (var connection = SpikeDb.Open(path, SpikeDb.RawKey(key)))
        {
            Assert.Equal("wal", connection.Scalar("PRAGMA journal_mode=WAL"));
            connection.Execute($"CREATE TABLE t(x TEXT); WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < 500) INSERT INTO t SELECT '{Marker}' || i FROM n;");

            // While the connection is open the new pages sit in the -wal file.
            Assert.True(new FileInfo(path + "-wal").Length > 0, "the write should be in the WAL before checkpoint");
            AssertNoMarker(temp);

            Assert.Equal(0L, connection.Scalar("PRAGMA wal_checkpoint(TRUNCATE)"));
            AssertNoMarker(temp);
        }

        AssertNoMarker(temp);
    }

    [Fact]
    public void Control_unkeyed_database_does_contain_the_marker()
    {
        // Proves the scan can find plaintext, so the encrypted test cannot pass vacuously.
        using var temp = new TempFolder();
        using (var connection = SpikeDb.Open(temp.File("plain.db"), password: null))
        {
            connection.Execute($"CREATE TABLE t(x TEXT); INSERT INTO t VALUES ('{Marker}');");
        }

        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertNoMarker(temp));
    }

    internal static void AssertNoMarker(TempFolder temp)
    {
        var marker = Encoding.UTF8.GetBytes(Marker);
        foreach (var file in Directory.GetFiles(temp.Path))
        {
            byte[] bytes;
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                bytes = new byte[stream.Length];
                stream.ReadExactly(bytes);
            }
            Assert.True(bytes.AsSpan().IndexOf(marker) < 0, $"plaintext marker found in {Path.GetFileName(file)}");
        }
    }
}
