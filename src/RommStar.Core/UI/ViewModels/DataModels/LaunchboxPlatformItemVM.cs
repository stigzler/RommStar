using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using RommStar.Core.Dtos.Romm;
using RommStar.Core.Models;
using RommStar.Core.UI.Messages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.ViewModels.DataModels;

public partial class LaunchboxPlatformItemVM : ObservableObject
{
    // Model related Observables
    [ObservableProperty]
    private RommServerItemVM?
        _assignedServerItem;

    [ObservableProperty]
    private string
        _launchboxPlatformName = string.Empty;

    [ObservableProperty]
    private string
        _launchboxPlatformRomFolder = string.Empty;


    [ObservableProperty]
    private string
        _romFolder = string.Empty;

    [ObservableProperty]
    private ObservableCollection<PlatformDTO>
        _matchedRommPlatforms = new ObservableCollection<PlatformDTO>();

    [ObservableProperty]
    private ExtendedSyncSettings 
        _extendedSyncSettings = new ExtendedSyncSettings();

    // Operational Observables

    [ObservableProperty]
    private bool
        _isOrphaned;

    [ObservableProperty]
    private string
        _iconPath = string.Empty;

    [ObservableProperty]
    private List<String>
        _errors = new List<string>();

    partial void OnLaunchboxPlatformRomFolderChanged(string value)
    {
        if (String.IsNullOrEmpty(value))
        {
            LaunchboxPlatformRomFolder = $"Games\\{LaunchboxPlatformName}";
        }
    }

    public LaunchboxPlatformItemVM()
    {
    }

    public LaunchboxPlatformItemVM(string name, string launchboxPlatformRomFolder)
    {
        LaunchboxPlatformName = name;

        // This controls for launchbox's odd behavior with blank rom folder names
        // (he must have hardcoded null > the path logic below. Naughty.)
        if (String.IsNullOrEmpty(launchboxPlatformRomFolder))
        {
            launchboxPlatformRomFolder = $"Games\\{LaunchboxPlatformName}";
        }
        LaunchboxPlatformRomFolder = launchboxPlatformRomFolder;
    }

    public void RefreshIcon()
    {
        OnPropertyChanged(nameof(IconPath));
    }

    [RelayCommand]
    private async Task DeleteOrphan()
    {
        WeakReferenceMessenger.Default.Send(new DeleteLaunchboxPlatformItemMessage(this));
    }
}