namespace MordWare.Core.Editions;

/// <summary>
/// Derives the running edition from the installed package's identity name, the same identity Windows
/// uses to separate each package's storage, so the edition and the data folder can never disagree.
/// </summary>
public static class AppEnvironmentResolver
{
    /// <summary>Package identity name of the PROD edition.</summary>
    public const string ProdPackageName = "MordWare";

    /// <summary>Package identity name of the DEV edition.</summary>
    public const string DevPackageName = "MordWare.Dev";

    /// <summary>
    /// Maps a package identity name to an edition. Only the exact PROD name yields <see cref="AppEnvironment.Prod"/>;
    /// anything else (the DEV name, an unknown name, or no package identity at all) yields
    /// <see cref="AppEnvironment.Dev"/>, so every doubt lands on the side that cannot touch real data.
    /// </summary>
    public static AppEnvironment FromPackageName(string? packageName) =>
        string.Equals(packageName, ProdPackageName, StringComparison.Ordinal)
            ? AppEnvironment.Prod
            : AppEnvironment.Dev;
}
