using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Windows.ApplicationModel.Resources;
using MordWare.Core.Editions;

namespace MordWare.App.ViewModels;

/// <summary>State of the main window: its localized title and whether the DEV marker is shown.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(AppEnvironment environment, ResourceLoader resources)
    {
        IsDev = environment == AppEnvironment.Dev;
        Title = resources.GetString(IsDev ? "AppTitleDev" : "AppTitle");
        DevBadgeText = resources.GetString("DevBadgeText");
        DevBadgeTooltip = resources.GetString("DevBadgeTooltip");
        IconPath = IsDev ? "Assets/Dev/AppIcon.ico" : "Assets/AppIcon.ico";
    }

    /// <summary>True only in the DEV edition: drives the DEV marker's visibility.</summary>
    public bool IsDev { get; }

    public string Title { get; }

    public string DevBadgeText { get; }

    public string DevBadgeTooltip { get; }

    /// <summary>Window and title-bar icon: the DEV edition uses its own icon set.</summary>
    public string IconPath { get; }
}
