using RommStar.Core.Models;
using System.Collections.Generic;
using System.Threading;

namespace RommStar.Core.Sync
{
    /// <summary>
    /// Internal task encapsulation carrying explicit self-contained execution state.
    /// </summary>
    public class PlatformSyncTask
    {
        public CancellationTokenSource Cts { get; set; } = new();

        public bool DownloadMediaFiles { get; set; } = false;

        public bool DownloadRomFiles { get; set; } = false;

        /// <summary>
        /// 2. This references the property on the UiCard above
        /// </summary>
        public Guid Id => UiCard.Id;

        public string LaunchBoxPlatformName { get; set; } = string.Empty;

        public string LaunchBoxRomFolder { get; set; } = string.Empty;
        public List<int> RommPlatformIds { get; set; } = new();
        public ExtendedSyncSettings SyncSettings { get; set; }

        /// <summary>
        /// Captures the specific server assigned to this run to eliminate cross-talk race conditions
        /// </summary>
        public RommServer TargetServer { get; set; } = null!;

        public PlatformSyncJob UiCard { get; set; } = null!;

        /// <summary>
        /// TODO: this is computable from SyncSettings, but best set once rather than on each romm iteration
        /// Depending on extent of SyncSettings, SyncSettings may be removable in the future. 
        /// </summary>
        public bool UpsertIGame { get; set; } = true;
    }
}