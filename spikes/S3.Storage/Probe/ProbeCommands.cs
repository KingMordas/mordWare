using System.Text.Json;

namespace S3.Storage.Probe;

/// <summary>
/// Design D2: one headless command per launch. The result goes to
/// Documents\mordWare S3 spike\results\&lt;tag or command&gt;.json, written atomically (.tmp + rename),
/// because a WinUI process has no console to answer on. The driver waits for that file.
/// </summary>
internal static class ProbeCommands
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static int Run(string[] commandLine)
    {
        var args = ProbeArgs.Parse(commandLine);
        var result = new Dictionary<string, object?>
        {
            ["command"] = args.Command,
            ["arguments"] = commandLine,
            ["startedUtc"] = DateTime.UtcNow.ToString("O"),
            ["processId"] = Environment.ProcessId,
        };

        int exitCode;
        try
        {
            switch (args.Command)
            {
                case "where":
                    Where(result);
                    break;
                case "write":
                    Write(args.Arg(0, "path"), args.Arg(1, "marker"), result);
                    break;
                case "read":
                    Read(args.Arg(0, "path"), result);
                    break;
                case "create-db":
                    result["recoveryKey"] = SpikeDatabase.Create(args.Edition);
                    SpikeDatabase.Open(args.Edition, result);
                    break;
                case "open-db":
                    SpikeDatabase.Open(args.Edition, result);
                    break;
                case "backup":
                    {
                        var folder = args.Positional.Count > 0 ? args.Positional[0] : SpikeFolders.Backups(args.Edition);
                        result["folder"] = folder;
                        var file = BackupFile.Write(folder, args.Edition, r => WriteResult(args.Tag ?? args.Command, r), args.Interrupt, result);
                        result["file"] = file;
                        result["tmpFilesLeft"] = Directory.GetFiles(folder, "*.tmp").Select(Path.GetFileName).ToArray();
                        break;
                    }
                case "restore":
                    {
                        // <file or folder> <recovery key>; a folder means its newest complete backup.
                        var source = args.Arg(0, "file or folder");
                        var file = Directory.Exists(source) ? BackupFile.Newest(source) : source;
                        result["file"] = file;
                        BackupFile.Restore(file, args.Arg(1, "recovery key"), args.Edition, result);
                        break;
                    }
                default:
                    throw new ArgumentException($"Unknown probe command '{args.Command}'.");
            }
            result["ok"] = true;
            exitCode = 0;
        }
        catch (Exception ex)
        {
            result["ok"] = false;
            result["error"] = $"{ex.GetType().FullName}: {ex.Message}";
            exitCode = 1;
        }

        WriteResult(args.Tag ?? args.Command, result);
        return exitCode;
    }

    /// <summary>Where the package's folders resolve, and whether Documents is synced by OneDrive.</summary>
    private static void Where(Dictionary<string, object?> result)
    {
        var oneDriveRoots = SpikeFolders.OneDriveRoots;
        result["localFolder"] = SpikeFolders.LocalState;
        result["documents"] = SpikeFolders.Documents;
        result["localAppData"] = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        result["oneDriveRoots"] = oneDriveRoots;
        result["documentsUnderOneDrive"] = oneDriveRoots.Any(root => SpikeFolders.IsUnder(SpikeFolders.Documents, root));
        result["packageFamilyName"] = Windows.ApplicationModel.Package.Current.Id.FamilyName;
        result["packageVersion"] = FormatVersion(Windows.ApplicationModel.Package.Current.Id.Version);
        result["osVersion"] = Environment.OSVersion.VersionString;
    }

    /// <summary>Writes a marker file (folders created as needed), then reports what the package reads back.</summary>
    private static void Write(string path, string marker, Dictionary<string, object?> result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, marker);
        Read(path, result);
    }

    /// <summary>Reports whether the package sees the file, and its content.</summary>
    private static void Read(string path, Dictionary<string, object?> result)
    {
        result["path"] = path;
        result["exists"] = File.Exists(path);
        result["content"] = File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static string FormatVersion(Windows.ApplicationModel.PackageVersion v) =>
        $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";

    private static void WriteResult(string name, Dictionary<string, object?> result)
    {
        Directory.CreateDirectory(SpikeFolders.Results);
        var path = Path.Combine(SpikeFolders.Results, name + ".json");
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(result, Json));
        File.Move(tmp, path, overwrite: true);
    }
}

/// <summary>command [positional...] [--edition DEV|PROD] [--tag name] [--interrupt]</summary>
internal sealed record ProbeArgs(string Command, IReadOnlyList<string> Positional, SpikeEdition Edition, string? Tag, bool Interrupt)
{
    public static ProbeArgs Parse(string[] commandLine)
    {
        var positional = new List<string>();
        var edition = SpikeEdition.Dev;
        string? tag = null;
        var interrupt = false;

        for (var i = 1; i < commandLine.Length; i++)
        {
            switch (commandLine[i])
            {
                case "--edition":
                    edition = Enum.Parse<SpikeEdition>(commandLine[++i], ignoreCase: true);
                    break;
                case "--tag":
                    tag = commandLine[++i];
                    break;
                case "--interrupt":
                    interrupt = true;
                    break;
                default:
                    positional.Add(commandLine[i]);
                    break;
            }
        }

        return new ProbeArgs(commandLine[0], positional, edition, tag, interrupt);
    }

    public string Arg(int index, string name) =>
        index < Positional.Count ? Positional[index] : throw new ArgumentException($"Missing argument <{name}>.");
}
