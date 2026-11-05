using System.Text.Json;

namespace S4.Installer.Probe;

/// <summary>
/// Check (e): unpackaged there is no ApplicationData / LocalSettings, so the picked folder is kept in
/// picker.json inside the edition's data root (the pattern Foundation's settings will need).
/// </summary>
public static class PickerStore
{
    private static string FilePath => Path.Combine(SpikeFolders.DataRoot, "picker.json");

    public static string? Load() =>
        File.Exists(FilePath) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath))?["path"] : null;

    public static void Save(string path)
    {
        Directory.CreateDirectory(SpikeFolders.DataRoot);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(new Dictionary<string, string> { ["path"] = path }));
    }
}
