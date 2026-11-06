using MordWare.Core.Editions;

namespace MordWare.App.Hosting;

/// <summary>
/// The app's <see cref="IAppPaths"/>: the edition's own data root directly under the account's local
/// application data, <c>%LOCALAPPDATA%\mordWare Data\</c> (PROD) or <c>%LOCALAPPDATA%\mordWare Data (DEV)\</c>
/// (DEV), named by <see cref="EditionProfile.DataFolderName"/>. The root is a sibling of the installer's
/// program folder (<c>%LOCALAPPDATA%\&lt;PackId&gt;\</c>), never inside it, so upgrading, reinstalling or
/// uninstalling never touches data. Never used by tests (architecture rule 4); tests use a temporary folder.
/// </summary>
public sealed class LocalAppDataPaths : IAppPaths
{
    public LocalAppDataPaths(EditionProfile edition)
        : this(edition, System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData, System.Environment.SpecialFolderOption.DoNotVerify))
    {
    }

    internal LocalAppDataPaths(EditionProfile edition, string localAppDataRoot)
    {
        if (string.IsNullOrWhiteSpace(localAppDataRoot))
        {
            throw new InvalidOperationException("The account's local application data folder is not available.");
        }

        Environment = edition.Environment;
        DataFolder = Directory.CreateDirectory(Path.Combine(localAppDataRoot, edition.DataFolderName)).FullName;
        LogsFolder = Directory.CreateDirectory(Path.Combine(DataFolder, "Logs")).FullName;
    }

    public AppEnvironment Environment { get; }

    /// <summary>The edition's data root itself: the database lives directly in it from Foundation onward.</summary>
    public string DataFolder { get; }

    public string LogsFolder { get; }
}
