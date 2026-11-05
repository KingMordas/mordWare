using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using S4.Installer.Probe;
using Velopack;

namespace S4.Installer;

/// <summary>
/// Design D2. Our own entry point (DISABLE_XAML_GENERATED_MAIN): Velopack's hooks run first, before
/// any UI, then either one headless probe command (D5) or the WinUI window, exactly as the
/// XAML-generated Main would start it.
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var started = DateTimeOffset.Now;
        VelopackApp.Build()
            .SetLogger(SpikeLog.Instance)
            .OnFirstRun(v =>
            {
                // Setup.exe starts the app once after installing. The spike records it and exits
                // instead of opening a window, so a silent install stays headless.
                Results.Write("firstrun", new { version = v.ToString(), started });
                Environment.Exit(0);
            })
            .OnRestarted(v => Results.Write("restarted", new { version = v.ToString(), started }))
            .Run();

        Results.Write("entry", new { velopackHookRanBeforeUi = true, args, started });

        if (args.Length > 0)
        {
            return ProbeCommands.Run(args);
        }

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
        return 0;
    }
}
