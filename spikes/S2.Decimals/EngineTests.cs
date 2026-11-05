namespace S2.Decimals;

/// <summary>Task 1.2: the spike runs on the encrypted SQLite3MC engine from S1.</summary>
public class EngineTests
{
    [Fact]
    public void Encrypted_database_opens_on_sqlite3mc()
    {
        using var temp = new TempFolder();
        using var connection = SpikeDb.Open(temp.File("engine.db"), SpikeDb.NewRawKey());

        Assert.False(string.IsNullOrEmpty(connection.Scalar("SELECT sqlite3mc_version()") as string));
    }
}
