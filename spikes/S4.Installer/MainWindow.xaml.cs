using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;
using S4.Installer.Probe;

namespace S4.Installer;

/// <summary>Check (e): the Windows App SDK folder picker, unpackaged; the chosen path lives in picker.json.</summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = SpikeEdition.Title;
        Info.Text = $"{SpikeEdition.Title} {ProbeCommands.AppVersion()} | data root: {SpikeFolders.DataRoot} | picked: {PickerStore.Load() ?? "(none)"}";
    }

    private async void OnPickFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FolderPicker(AppWindow.Id);
            var folder = await picker.PickSingleFolderAsync();
            if (folder is null)
            {
                Status.Text = "Cancelled.";
                return;
            }

            PickerStore.Save(folder.Path);
            Results.Write("pick", new { path = folder.Path });
            Status.Text = $"Picked {folder.Path}";
        }
        catch (Exception ex)
        {
            Results.Write("pick", new { error = ex.ToString() });
            Status.Text = ex.Message;
        }
    }

    private void OnWriteTestFile(object sender, RoutedEventArgs e)
    {
        var folder = PickerStore.Load();
        if (folder is null)
        {
            Status.Text = "Pick a folder first.";
            return;
        }

        try
        {
            var file = Path.Combine(folder, $"s4-picker-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(file, "mordWare S4 spike picker test");
            Results.Write("picker-write", new { file, exists = File.Exists(file) });
            Status.Text = $"Wrote {Path.GetFileName(file)}";
        }
        catch (Exception ex)
        {
            Results.Write("picker-write", new { error = ex.ToString() });
            Status.Text = ex.Message;
        }
    }

    private async void OnCheckForUpdates(object sender, RoutedEventArgs e)
    {
        Status.Text = "Checking...";
        var feed = Environment.GetEnvironmentVariable("S4_SPIKE_FEED") ?? "github";
        var code = await ProbeCommands.CheckUpdateAsync(feed, apply: false);
        Status.Text = $"check-update exit {code}; see results/check-update.json";
    }
}
