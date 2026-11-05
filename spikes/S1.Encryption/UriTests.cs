namespace S1.Encryption;

/// <summary>Design D3: database paths are percent-encoded into the SQLite URI.</summary>
public class UriTests
{
    [Fact]
    public void Path_with_space_and_hash_is_encoded_and_opens_the_intended_file()
    {
        using var temp = new TempFolder(); // its folder name already contains a space
        var path = temp.File("my data #1.db");

        var uri = SpikeDb.CipherUri(path);
        Assert.DoesNotContain(' ', uri);
        Assert.Contains("%20", uri);
        Assert.Contains("%231.db", uri);

        using (var connection = SpikeDb.Open(path, "uri-password"))
        {
            connection.Execute("CREATE TABLE t(x); INSERT INTO t VALUES (42);");
        }

        Assert.True(File.Exists(path), "the database must be created at the decoded path, not a mangled one");
        using var reopened = SpikeDb.Open(path, "uri-password");
        Assert.Equal(42L, reopened.Scalar("SELECT x FROM t"));
    }
}
