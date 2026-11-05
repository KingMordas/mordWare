using MordWare.Core.Editions;

namespace MordWare.Tests.Unit.TestSupport;

/// <summary>
/// Test-only <see cref="IAppPaths"/>: a unique folder under the system temp directory, created per
/// instance and deleted on dispose. Tests must use this and never the app's real paths provider
/// (enforced by the architecture tests).
/// </summary>
public sealed class TempAppPaths : IAppPaths, IDisposable
{
    private readonly string _root;

    public TempAppPaths()
    {
        _root = Path.Combine(Path.GetTempPath(), "mordware-tests", Guid.NewGuid().ToString("N"));
        DataFolder = Directory.CreateDirectory(Path.Combine(_root, "Data")).FullName;
        LogsFolder = Directory.CreateDirectory(Path.Combine(_root, "Logs")).FullName;
    }

    /// <summary>Tests always run as DEV.</summary>
    public AppEnvironment Environment => AppEnvironment.Dev;

    public string DataFolder { get; }

    public string LogsFolder { get; }

    /// <summary>The per-instance root folder (parent of every resolved folder).</summary>
    public string Root => _root;

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
