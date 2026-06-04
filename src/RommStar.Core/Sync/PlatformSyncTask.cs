using System.Collections.Generic;
using System.Threading;

namespace RommStar.Core.Sync
{
    /// <summary>
    /// Holds the structural execution context for an entire macro platform sync run.
    /// </summary>
    public class PlatformSyncTask
    {
        public string LaunchBoxPlatformName { get; set; } = string.Empty;
        public List<int> RommPlatformIds { get; set; } = new();
        public bool DownloadRomFiles { get; set; }
        public PlatformSyncJob UiCard { get; set; } = null!;
        public CancellationTokenSource Cts { get; set; } = new();
    }
}
