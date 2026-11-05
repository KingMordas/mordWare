using Microsoft.UI.Xaml;
using S3.Storage.Probe;

namespace S3.Storage;

/// <summary>
/// Design D1. With a command line (launched through the s3probe.exe alias), run one probe command
/// headless, write its JSON result and exit. Without one, open the picker window (check e).
/// </summary>
public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Environment.GetCommandLineArgs()[0] is the executable; the rest are the probe command.
        var commandLine = Environment.GetCommandLineArgs().Skip(1).ToArray();
        if (commandLine.Length > 0)
        {
            var exitCode = ProbeCommands.Run(commandLine);
            Environment.Exit(exitCode);
            return;
        }

        _window = new PickerWindow();
        _window.Activate();
    }
}
