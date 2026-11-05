using Velopack.Logging;

namespace S4.Installer.Probe;

/// <summary>Velopack's log, appended to results\velopack.log. Synthetic spike values only.</summary>
public sealed class SpikeLog : IVelopackLogger
{
    public static SpikeLog Instance { get; } = new();

    private static readonly object Gate = new();

    public void Log(VelopackLogLevel logLevel, string? message, Exception? exception)
    {
        lock (Gate)
        {
            File.AppendAllText(
                Path.Combine(SpikeFolders.Results, "velopack.log"),
                $"{DateTimeOffset.Now:O} [{logLevel}] {message}{(exception is null ? "" : " | " + exception.GetType().Name + ": " + exception.Message)}{Environment.NewLine}");
        }
    }
}
