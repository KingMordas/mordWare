using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using MordWare.Core.Editions;
using Velopack;

namespace MordWare.App;

/// <summary>
/// The app's entry point (the project defines <c>DISABLE_XAML_GENERATED_MAIN</c>). Velopack's install,
/// update and uninstall hooks run first, before any UI; they exit the process when the installer
/// started it for a hook. Then the app language is applied, and WinUI starts exactly as the XAML-generated Main would.
/// No logger is attached to Velopack: its messages contain user-profile paths (guardrail 6).
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main()
    {
        VelopackApp.Build().Run();

        // Language first (guardrail 3), before XAML creates the resource context; then App.OnLaunched
        // builds the host and the window.
        App.ApplyAppLanguage(AppLanguages.Default);

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}
