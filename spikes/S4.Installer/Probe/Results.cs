using System.Text.Json;

namespace S4.Installer.Probe;

/// <summary>Each probe command writes results\&lt;name&gt;.json; the driver reads it from outside.</summary>
public static class Results
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static void Write(string name, object value)
    {
        var path = Path.Combine(SpikeFolders.Results, name + ".json");
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
        File.Move(tmp, path, overwrite: true);
    }
}
