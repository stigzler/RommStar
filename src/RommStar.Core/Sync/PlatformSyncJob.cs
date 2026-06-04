using CommunityToolkit.Mvvm.ComponentModel;

namespace RommStar.Core.Sync
{
    /// <summary>
    /// Observable Model designed to bind directly to iNKORE Card layout components.
    /// </summary>
    public partial class PlatformSyncJob : ObservableObject
    {
        [ObservableProperty]
        private SyncStatus _status;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ProgressPercentage))]
        private int _totalItems;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ProgressPercentage))]
        private int _processedItems;

        [ObservableProperty]
        private int _errorCount;

        [ObservableProperty]
        private int _warningCount;

        public string LaunchBoxPlatformName { get; set; } = string.Empty;
        public string ServerName { get; set; } = string.Empty;

        public double ProgressPercentage => TotalItems == 0 ? 0 : ((double)ProcessedItems / TotalItems) * 100;
    }
}
