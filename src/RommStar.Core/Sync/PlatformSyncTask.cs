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

        /// <summary>
        /// Whether Game Media should be downloaded with this sync
        /// </summary>
        public bool DownloadMediaFiles { get; set; } = false;

        /// <summary>
        /// Whether Rom (and soundtrack) files should be downloaded with this sync
        /// </summary>
        public bool DownloadRomFiles { get; set; } = false;

        /// <summary>
        /// The launchbox EmulatorID (used in upserting iGame as needs emulator against it)
        /// </summary>
        public string? EmulatorID { get; set; }

        /// <summary>
        /// GUID of the Sync Jobs UICard
        /// </summary>
        public Guid Id => UiCard.Id;
        public string LaunchBoxRomFolder { get; set; } = string.Empty;
        public bool NotifyLauncboxWhenMetadataComplete { get; set; } = false;
        public IPlatformFolder[] PlatformMediaFolders { get; set; }
        public string PlatformName { get; set; } = string.Empty;
        public List<int> RommPlatformIds { get; set; } = new();
        public ExtendedSyncSettings SyncSettings { get; set; }

        /// <summary>
        /// Captures the specific server assigned to this run to eliminate cross-talk race conditions
        /// </summary>
        public RommServer TargetServer { get; set; } = null!;
        public PlatformSyncCardVM UiCard { get; set; } = null!;

        /// <summary>
        /// This is whether Sync should Insert/Create an IGame based on the Romm Game/Rom
        /// </summary>
        public bool UpdateMetadata { get; set; } = true;
    }
}