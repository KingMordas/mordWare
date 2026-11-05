namespace S2.Decimals;

/// <summary>A unique folder under %TEMP% for one test, deleted (with everything in it) on dispose.</summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mordware-s2 " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        // Pooling is off in SpikeDb, but EF or a stray connection may still hold a handle.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Directory.Delete(Path, recursive: true);
        }
    }
}
