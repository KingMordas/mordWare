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

    /// <summary>Resolves the edition from the running package's identity and creates its paths.</summary>
    public static AppHost Create()
    {
        var environment = AppEnvironmentResolver.FromPackageName(TryGetPackageName());
        return new AppHost(environment, new PackagedAppPaths(environment));
    }

    private static string? TryGetPackageName()
    {
        try
        {
            return Windows.ApplicationModel.Package.Current.Id.Name;
        }
        catch (InvalidOperationException)
        {
            // No package identity (should not happen: mordWare always runs packaged). Resolves to DEV.
            return null;
        }
    }
}
