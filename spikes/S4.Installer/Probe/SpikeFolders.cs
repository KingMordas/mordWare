namespace S4.Installer.Probe;

/// <summary>
/// Design D3: the spike's data root lives directly under %LOCALAPPDATA%, as a sibling of (never
/// inside) Velopack's own %LOCALAPPDATA%\&lt;packId&gt; folder, one per edition.
/// </summary>
public static class SpikeFolders
{
    public static string LocalAppData { get; } =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string DataRoot { get; } = Path.Combine(
        LocalAppData, SpikeEdition.IsProd ? "mordWare S4 spike Data" : "mordWare S4 spike Data (DEV)");

    public static string Results => Directory.CreateDirectory(Path.Combine(DataRoot, "results")).FullName;

    public static string Marker => Path.Combine(DataRoot, "marker.txt");
}
