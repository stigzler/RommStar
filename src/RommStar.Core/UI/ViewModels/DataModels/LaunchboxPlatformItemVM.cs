using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using RommStar.Core.Dtos;
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
    private RommServerItemVM? _assignedServerItem;

    [ObservableProperty]
    private string _launchboxPlatformName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MappedRommPlatformsCount))]
    private ObservableCollection<RommPlatformDTO> _matchedRommPlatforms = new ObservableCollection<RommPlatformDTO>();

    // Operational Observables

    [ObservableProperty]
    private bool _isOrphaned;

    [ObservableProperty]
    private string _iconPath = string.Empty;

    [ObservableProperty]
    private List<String> _errors = new List<string>();

    public int MappedRommPlatformsCount => MatchedRommPlatforms.Count();

    public LaunchboxPlatformItemVM()
    {
    }

    public LaunchboxPlatformItemVM(string name)
    {
        LaunchboxPlatformName = name;
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