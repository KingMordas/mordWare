namespace S3.Storage.Probe;

/// <summary>Stand-in for MordWare.Core's AppEnvironment; the spike stays standalone (design D1).</summary>
internal enum SpikeEdition
{
    Prod,
    Dev,
}

/// <summary>
/// Design D4: the folders the spike uses. All live under "mordWare S3 spike" names and are deleted
/// by run-checks.ps1 at the end; the real "mordWare Backups" folders are never touched.
/// </summary>
internal static class SpikeFolders
{
    public const string RootName = "mordWare S3 spike";

    /// <summary>Documents, resolved the way Foundation would (honors a Known Folder Move into OneDrive).</summary>
    public static string Documents => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    public static string Root => Path.Combine(Documents, RootName);

    public static string Results => Path.Combine(Root, "results");

    /// <summary>Per-edition default backup folder, mirroring "mordWare Backups" / "mordWare Backups (DEV)".</summary>
    public static string Backups(SpikeEdition edition) =>
        Path.Combine(Root, edition == SpikeEdition.Dev ? "Backups (DEV)" : "Backups");

    /// <summary>The package's LocalState (ApplicationData.LocalFolder).</summary>
    public static string LocalState => Windows.Storage.ApplicationData.Current.LocalFolder.Path;

    public static string DataFolder => Directory.CreateDirectory(Path.Combine(LocalState, "Data")).FullName;

    /// <summary>OneDrive roots announced by the OneDrive client, if any.</summary>
    public static IReadOnlyList<string> OneDriveRoots =>
        new[] { "OneDrive", "OneDriveConsumer", "OneDriveCommercial" }
            .Select(Environment.GetEnvironmentVariable)
            .Where(v => !string.IsNullOrEmpty(v))
            .Select(v => Path.TrimEndingDirectorySeparator(v!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static bool IsUnder(string path, string root) =>
        Path.GetFullPath(path).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
