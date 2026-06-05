using System;
using RommStar.Core.Models;

namespace RommStar.Core.Sync
{
    /// <summary>
    /// Represents an isolated, micro-level file streaming payload.
    /// </summary>
    public class DownloadJob
    {
        /// <summary>
        /// The individual file download needs to know which exact Guid run it belongs to
        /// </summary>
        public Guid JobId { get; set; }

        public DownloadJobType JobType { get; set; }
        public string RelativeUrl { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public string LaunchBoxPlatformName { get; set; } = string.Empty;
        public RommServerConfig ServerContext { get; set; } = null!;
        public PlatformSyncJob? UiCard { get; set; } // Null if bypassed via on-demand
        public Action? OnSuccessCallback { get; set; }
        public CancellationToken CancellationToken { get; set; }
    }
}