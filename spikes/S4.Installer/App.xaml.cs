using Microsoft.UI.Xaml;
using S4.Installer.Probe;

namespace S4.Installer;

/// <summary>Opens the probe window (checks e, j and the manual part of f).</summary>
public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Language.Apply("en-US");
        _window = new MainWindow();
        _window.Activate();
        Results.Write("window", new { title = _window.Title, edition = SpikeEdition.Name });
    }
}
