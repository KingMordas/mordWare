using System.Globalization;
using Microsoft.UI.Xaml;
using MordWare.App.Hosting;
using MordWare.Core.Editions;

namespace MordWare.App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    /// <summary>The composition root for this run (edition and data paths).</summary>
    public static AppHost Host { get; private set; } = null!;

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        ApplyAppLanguage(AppLanguages.Default);
        Host = AppHost.Create();
        _window = new MainWindow();
        _window.Activate();
    }

    /// <summary>
    /// Makes the app's selected language, never the OS, drive resources and formatting (guardrail 3).
    /// Set on every start: Windows persists the override per package, so a stale value must never win.
    /// The foundation change replaces the hard-coded default with the user's language setting.
    /// </summary>
    private static void ApplyAppLanguage(string languageTag)
    {
        var culture = AppLanguages.Culture(languageTag);
        Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = culture.Name;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
