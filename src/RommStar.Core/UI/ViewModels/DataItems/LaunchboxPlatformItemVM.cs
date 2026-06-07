using CommunityToolkit.Mvvm.ComponentModel;
using RommStar.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.UI.ViewModels.DataItems
{
    public partial class LaunchboxPlatformItemVM : ObservableObject
    {
        // Model related Observables
        [ObservableProperty]
        private RommServer? _assignedServer;

        [ObservableProperty]
        private string _launchboxPlatformName = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MappedRommPlatformsCount))]
        private List<int> _matchedRommPlatforms = new List<int>();

        // Operational Observables

        [ObservableProperty]
        private bool _isOrphaned;

        [ObservableProperty]
        private string _iconPath = string.Empty;

        [ObservableProperty]
        private List<String> _errors = new List<string>();

        public int MappedRommPlatformsCount => MatchedRommPlatforms.Count();
    }
}