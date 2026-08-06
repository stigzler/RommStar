using RommStar.Core.Models;
using System.Collections.Generic;
using System.Threading;
using Unbroken.LaunchBox.Plugins.Data;

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
        public string? EmulatorID { get; set; }
        public bool NotifyLauncboxWhenMetadataComplete { get; set; } = false;

        /// <summary>
        /// 2. This references the property on the UiCard above
        /// </summary>
        public Guid Id => UiCard.Id;
        public string PlatformName { get; set; } = string.Empty;
        public string LaunchBoxRomFolder { get; set; } = string.Empty;

        public IPlatformFolder[] PlatformMediaFolders { get; set; }
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