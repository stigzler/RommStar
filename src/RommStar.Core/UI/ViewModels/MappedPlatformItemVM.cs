using CommunityToolkit.Mvvm.ComponentModel;
using RommStar.Core.Dtos;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.ViewModels
{
    public partial class MappedPlatformItemVM : ObservableObject
    {
        // The current LaunchBox platform name (the Dictionary key)
        [ObservableProperty] private string _launchboxPlatformName = string.Empty;

        // Visual helper flags that never touch disk storage
        [ObservableProperty] private bool _isOrphaned;

        [ObservableProperty] private string _iconPath = string.Empty;

        // The live mapped RomM entities assigned to this LaunchBox platform
        public ObservableCollection<RommPlatformDTO> MappedRommPlatforms { get; } = new();

        public MappedPlatformItemVM()
        {
        }

        public MappedPlatformItemVM(string name, bool isOrphaned)
        {
            _launchboxPlatformName = name;
            _isOrphaned = isOrphaned;
        }
    }
}