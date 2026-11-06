using MordWare.Core.Editions;

namespace MordWare.App.Hosting;

/// <summary>
/// Minimal composition root, built once at startup: which edition is running and where its files live.
/// Grows into proper dependency injection with the foundation change.
/// </summary>
public sealed class AppHost
{
    private AppHost(AppEnvironment environment, IAppPaths paths)
    {
        Environment = environment;
        Paths = paths;
    }

    public AppEnvironment Environment { get; }

    public IAppPaths Paths { get; }

    /// <summary>Takes the edition this build is (<see cref="AppEdition"/>) and creates its data paths.</summary>
    public static AppHost Create()
    {
        var edition = AppEdition.Profile;
        return new AppHost(edition.Environment, new LocalAppDataPaths(edition));
    }
}
