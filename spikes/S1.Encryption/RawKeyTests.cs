using System.Security.Cryptography;
using System.Text;

namespace S1.Encryption;

/// <summary>Design D4: the 32-byte data key is used as a SQLCipher raw key through the Password keyword.</summary>
public class RawKeyTests
{
    private static readonly byte[] PlainHeader = Encoding.ASCII.GetBytes("SQLite format 3\0");

    [Fact]
    public void Raw_key_through_Password_creates_an_encrypted_database_that_reopens()
    {
        using var temp = new TempFolder();
        var path = temp.File("raw.db");
        var key = RandomNumberGenerator.GetBytes(32);

        using (var connection = SpikeDb.Open(path, SpikeDb.RawKey(key)))
        {
            connection.Execute("CREATE TABLE t(x); INSERT INTO t VALUES ('hello');");
        }

        // The file is not a plain SQLite database: its header is ciphertext (SQLCipher keeps a random salt there).
        var header = File.ReadAllBytes(path).AsSpan(0, 16).ToArray();
        Assert.NotEqual(PlainHeader, header);

        using var reopened = SpikeDb.Open(path, SpikeDb.RawKey(key));
        Assert.Equal("hello", reopened.Scalar("SELECT x FROM t"));
    }

    [Fact]
    public void Raw_key_is_interpreted_as_key_bytes_not_as_a_passphrase()
    {
        // If "x'..'" were treated as an ordinary passphrase, SQLCipher would derive the same key from the
        // same string and a lower-case hex variant would fail. As a raw key, hex case cannot matter.
        using var temp = new TempFolder();
        var path = temp.File("raw-case.db");
        var key = RandomNumberGenerator.GetBytes(32);

        using (var connection = SpikeDb.Open(path, SpikeDb.RawKey(key)))
        {
            connection.Execute("CREATE TABLE t(x); INSERT INTO t VALUES (1);");
        }

        var lowerCaseRawKey = "x'" + Convert.ToHexString(key).ToLowerInvariant() + "'";
        using var reopened = SpikeDb.Open(path, lowerCaseRawKey);
        Assert.Equal(1L, reopened.Scalar("SELECT x FROM t"));
    }
}
