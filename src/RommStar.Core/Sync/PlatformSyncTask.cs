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
        // 2. This now safely references the property on the UiCard above
        public Guid Id => UiCard.Id;

        public string LaunchBoxPlatformName { get; set; } = string.Empty;
        public List<int> RommPlatformIds { get; set; } = new();
        public bool DownloadRomFiles { get; set; }
        public PlatformSyncJob UiCard { get; set; } = null!;
        public CancellationTokenSource Cts { get; set; } = new();

        /// <summary>
        /// Captures the specific server assigned to this run to eliminate cross-talk race conditions
        /// </summary>
        public RommServer TargetServer { get; set; } = null!;
    }
}