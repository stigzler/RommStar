using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using RommStar.Core.Dtos;
using RommStar.Core.Models;

namespace RommStar.Core.UI.ViewModels
{
    public partial class MappedPlatformItemVM : ObservableObject
    {
        [ObservableProperty] private string _launchboxPlatformName = string.Empty;
        [ObservableProperty] private bool _isOrphaned;
        [ObservableProperty] private string _iconPath = string.Empty;
        [ObservableProperty] private RommServer? _assignedServer;

        // Rich DTOs exposed directly so the View can display categories, slugs, and sizes
        public ObservableCollection<RommPlatformDTO> MappedRommPlatforms { get; } = new();

        // Server-Local IDs stored out of your user settings JSON
        public List<int> StoredRommPlatformIds { get; set; } = new();

        public MappedPlatformItemVM(string name, bool isOrphaned)
        {
            LaunchboxPlatformName = name;
            IsOrphaned = isOrphaned;
        }
    }
}