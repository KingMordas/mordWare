using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.ApplicationModel.Resources;
using MordWare.App.ViewModels;

namespace MordWare.App;

/// <summary>
/// The application window. Hosts a Frame that displays pages; window state lives in
/// <see cref="MainWindowViewModel"/>, so this code-behind only wires the title bar and icon.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        ViewModel = new MainWindowViewModel(App.Host.Environment, new ResourceLoader());
        InitializeComponent();

        Title = ViewModel.Title;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, ViewModel.IconPath));
        TitleBarIcon.ImageSource = new BitmapImage(new Uri($"ms-appx:///{ViewModel.IconPath}"));

        RootFrame.Navigate(typeof(MainPage));
    }

    public MainWindowViewModel ViewModel { get; }
}
