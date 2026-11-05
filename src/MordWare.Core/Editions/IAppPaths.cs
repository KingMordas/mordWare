namespace MordWare.Core.Editions;

/// <summary>
/// Where the running edition keeps its files. Code never builds data paths itself: it asks this
/// abstraction, so the installed app resolves its own per-package folder while tests get a temporary one.
/// Backup locations are added by the foundation change.
/// </summary>
public interface IAppPaths
{
    /// <summary>The edition these paths belong to.</summary>
    AppEnvironment Environment { get; }

    /// <summary>Folder holding the edition's live data (the database, from foundation onward).</summary>
    string DataFolder { get; }

    /// <summary>Folder holding the edition's log files (no personal or financial data, ever).</summary>
    string LogsFolder { get; }
}
