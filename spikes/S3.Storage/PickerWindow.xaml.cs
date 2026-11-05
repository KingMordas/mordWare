using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;
using Windows.Storage;

namespace S3.Storage;

/// <summary>
/// Check (e), design D4: pick a folder with the Windows App SDK picker, remember it in LocalSettings,
/// and write a test file there, before and after a restart of the probe.
/// </summary>
public sealed partial class PickerWindow : Window
{
    private const string FolderSetting = "S3.BackupFolder";

    public PickerWindow()
    {
        InitializeComponent();
        ShowFolder();
    }

    private static string? RememberedFolder =>
        ApplicationData.Current.LocalSettings.Values[FolderSetting] as string;

    private void ShowFolder() =>
        FolderText.Text = RememberedFolder is { } folder ? $"Remembered folder: {folder}" : "No folder remembered yet.";

    private async void OnPick(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FolderPicker(AppWindow.Id);
            var result = await picker.PickSingleFolderAsync();
            if (result is null)
            {
                StatusText.Text = "Picker cancelled.";
                return;
            }

            ApplicationData.Current.LocalSettings.Values[FolderSetting] = result.Path;
            ShowFolder();
            StatusText.Text = "Folder remembered.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Picker failed: {ex.GetType().Name}: {ex.Message}";
        }
    }

    private void OnWrite(object sender, RoutedEventArgs e)
    {
        if (RememberedFolder is not { } folder)
        {
            StatusText.Text = "Pick a folder first.";
            return;
        }

        try
        {
            var path = Path.Combine(folder, $"s3-picker-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(path, $"S3 spike picker check, written {DateTime.UtcNow:O}");
            StatusText.Text = $"Wrote {path}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Write failed: {ex.GetType().Name}: {ex.Message}";
        }
    }
}
