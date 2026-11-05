using MordWare.Core.Editions;
using Windows.Storage;

namespace MordWare.App.Hosting;

/// <summary>
/// The installed app's <see cref="IAppPaths"/>: folders under the package's own LocalState
/// (<see cref="ApplicationData.LocalFolder"/>). Windows scopes that folder per package identity, so
/// the PROD (<c>MordWare</c>) and DEV (<c>MordWare.Dev</c>) editions can never resolve each other's data.
/// Never used by tests (architecture rule); tests use a temporary-folder implementation instead.
/// </summary>
public sealed class PackagedAppPaths : IAppPaths
{
    public PackagedAppPaths(AppEnvironment environment)
    {
        Environment = environment;
        var root = ApplicationData.Current.LocalFolder.Path;
        DataFolder = Directory.CreateDirectory(Path.Combine(root, "Data")).FullName;
        LogsFolder = Directory.CreateDirectory(Path.Combine(root, "Logs")).FullName;
    }

    public AppEnvironment Environment { get; }

    public string DataFolder { get; }

    public string LogsFolder { get; }
}
