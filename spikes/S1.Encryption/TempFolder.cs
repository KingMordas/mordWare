namespace S1.Encryption;

/// <summary>A unique folder under %TEMP% for one test, deleted (with everything in it) on dispose.</summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        // The space is deliberate: every database path in the spike must survive URI encoding.
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mordware-s1 " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        // SQLite connection pooling can keep file handles open after Close(); release them first.
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
