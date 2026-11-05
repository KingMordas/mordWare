using System.Reflection;
using Microsoft.Windows.ApplicationModel.Resources;
using Velopack;
using Velopack.Sources;

namespace S4.Installer.Probe;

/// <summary>Design D5: one headless command per run; each writes results\&lt;command&gt;.json and exits.</summary>
public static class ProbeCommands
{
    /// <summary>The public repository: the only GitHub feed the spike reads (check h).</summary>
    public const string RepositoryUrl = "https://github.com/KingMordas/mordWare";

    public const string Channel = "s4-spike";

    public static string AppVersion() =>
        typeof(ProbeCommands).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";

    public static int Run(string[] args)
    {
        try
        {
            return args[0] switch
            {
                "where" => Where(),
                "version" => Version(),
                "write-marker" => WriteMarker(),
                "read-marker" => ReadMarker(),
                "lang" => Lang(args[1]),
                "check-update" => CheckUpdateAsync(Arg(args, "--feed") ?? "github", args.Contains("--apply")).GetAwaiter().GetResult(),
                _ => Unknown(args[0]),
            };
        }
        catch (Exception ex)
        {
            Results.Write(args[0] + "-error", new { error = ex.GetType().FullName, ex.Message, stack = ex.StackTrace });
            return 1;
        }
    }

    private static string? Arg(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static int Unknown(string command)
    {
        Results.Write("unknown", new { command });
        return 2;
    }

    /// <summary>An UpdateManager over an empty source: enough to read IsInstalled, AppId and version.</summary>
    private static UpdateManager Manager(IUpdateSource? source = null) =>
        new(source ?? new SimpleFileSource(new DirectoryInfo(Path.GetTempPath())), new UpdateOptions { ExplicitChannel = Channel });

    private static int Where()
    {
        var um = Manager();
        Results.Write("where", new
        {
            edition = SpikeEdition.Name,
            expectedPackId = SpikeEdition.PackId,
            title = SpikeEdition.Title,
            isInstalled = um.IsInstalled,
            appId = um.IsInstalled ? um.AppId : null,
            installedVersion = um.IsInstalled ? um.CurrentVersion?.ToString() : null,
            assemblyVersion = AppVersion(),
            exePath = Environment.ProcessPath,
            baseDirectory = AppContext.BaseDirectory,
            dataRoot = SpikeFolders.DataRoot,
            elevated = Environment.IsPrivilegedProcess,
            os = Environment.OSVersion.VersionString,
            arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
        });
        return 0;
    }

    private static int Version()
    {
        var um = Manager();
        Results.Write("version", new
        {
            assemblyVersion = AppVersion(),
            installedVersion = um.IsInstalled ? um.CurrentVersion?.ToString() : null,
            exePath = Environment.ProcessPath,
        });
        return 0;
    }

    private static int WriteMarker()
    {
        Directory.CreateDirectory(SpikeFolders.DataRoot);
        var content = $"S4 marker {SpikeEdition.Name} {AppVersion()} {DateTimeOffset.Now:O}";
        File.WriteAllText(SpikeFolders.Marker, content);
        Results.Write("write-marker", new { path = SpikeFolders.Marker, content });
        return 0;
    }

    private static int ReadMarker()
    {
        var exists = File.Exists(SpikeFolders.Marker);
        Results.Write("read-marker", new { path = SpikeFolders.Marker, exists, content = exists ? File.ReadAllText(SpikeFolders.Marker) : null });
        return exists ? 0 : 3;
    }

    /// <summary>Check (d): language override and .resw resources without package identity.</summary>
    private static int Lang(string tag)
    {
        string? legacyError = null;
        try
        {
            // The Windows.Globalization override the app uses today: expected to throw unpackaged.
            Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = tag;
        }
        catch (Exception ex)
        {
            legacyError = $"{ex.GetType().FullName}: {ex.Message}";
        }

        Language.Apply(tag);
        var loader = new ResourceLoader();
        Results.Write("lang-" + tag, new
        {
            tag,
            overrideNow = Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride,
            greeting = loader.GetString("SpikeGreeting"),
            devMarker = loader.GetString("SpikeDevMarker"),
            formattedDate = new DateTime(2026, 11, 5).ToString("D"),
            formattedNumber = 1234.5m.ToString("N2"),
            windowsGlobalizationOverride = legacyError ?? "no exception",
            resourceFile = ResourceLoader.GetDefaultResourceFilePath(),
        });
        return 0;
    }

    /// <summary>Checks (g) and (h): find, download and apply an update from a local folder or GitHub.</summary>
    public static async Task<int> CheckUpdateAsync(string feed, bool apply)
    {
        var downloader = new LoggingDownloader();
        IUpdateSource source = feed == "github"
            ? new GithubSource(RepositoryUrl, accessToken: null, prerelease: true, downloader)
            : new SimpleFileSource(new DirectoryInfo(feed));
        var um = Manager(source);
        if (!um.IsInstalled)
        {
            // The DEV-from-build-output path: no install, so updates are off (check d).
            Results.Write("check-update", new { feed, isInstalled = false, skipped = "not installed: update checks are off" });
            return 0;
        }

        var info = await um.CheckForUpdatesAsync();
        var result = new Dictionary<string, object?>
        {
            ["feed"] = feed == "github" ? "github" : "local",
            ["channel"] = Channel,
            ["currentVersion"] = um.CurrentVersion?.ToString(),
            ["found"] = info?.TargetFullRelease.Version.ToString(),
            ["baseRelease"] = info?.BaseRelease?.Version.ToString(),
            ["deltas"] = info?.DeltasToTarget.Select(d => new { d.FileName, d.Size }).ToArray(),
            ["fullSize"] = info?.TargetFullRelease.Size,
            ["applied"] = false,
            ["requests"] = downloader.Requests,
        };
        Results.Write("check-update", result);
        if (info is null || !apply)
        {
            return 0;
        }

        await um.DownloadUpdatesAsync(info);
        result["downloaded"] = true;
        result["applied"] = true;
        result["requests"] = downloader.Requests;
        Results.Write("check-update", result);

        // Restart headless into "version", so the driver can read the new version from outside.
        um.ApplyUpdatesAndRestart(info, ["version"]);
        return 0;
    }
}
