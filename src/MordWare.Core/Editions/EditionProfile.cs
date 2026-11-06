namespace MordWare.Core.Editions;

/// <summary>
/// The fixed names of one edition: its installer identity and its data folder. The edition itself is
/// fixed by the build (Release = PROD, every other configuration = DEV), never at run time; this type
/// only maps it to names. See docs/architecture.md, "Editions".
/// <c>scripts/pack.ps1</c> and <c>scripts/run-dev.ps1</c> keep their own copy of these names, each guarded.
/// </summary>
public sealed record EditionProfile
{
    /// <summary>The PROD edition: what users install.</summary>
    public static EditionProfile Prod { get; } = new(AppEnvironment.Prod, "MordWare", "mordWare", "mordWare Data");

    /// <summary>The DEV edition: runs from the build output, installed only for local installer checks.</summary>
    public static EditionProfile Dev { get; } = new(AppEnvironment.Dev, "MordWare.Dev", "mordWare (DEV)", "mordWare Data (DEV)");

    private EditionProfile(AppEnvironment environment, string packId, string installerTitle, string dataFolderName)
    {
        Environment = environment;
        PackId = packId;
        InstallerTitle = installerTitle;
        DataFolderName = dataFolderName;
    }

    /// <summary>The edition these names belong to.</summary>
    public AppEnvironment Environment { get; }

    /// <summary>
    /// The installer's package id (Velopack packId). The installer keeps the program files in
    /// <c>%LOCALAPPDATA%\&lt;PackId&gt;\</c>, which never contains a data folder.
    /// </summary>
    public string PackId { get; }

    /// <summary>The installed app's name in the Start menu and the installed-apps list.</summary>
    public string InstallerTitle { get; }

    /// <summary>The name of the edition's data folder, directly under the account's local application data.</summary>
    public string DataFolderName { get; }

    /// <summary>The names of <paramref name="environment"/>. An unknown value is refused, never guessed.</summary>
    public static EditionProfile For(AppEnvironment environment) => environment switch
    {
        AppEnvironment.Prod => Prod,
        AppEnvironment.Dev => Dev,
        _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, "Unknown edition."),
    };
}
