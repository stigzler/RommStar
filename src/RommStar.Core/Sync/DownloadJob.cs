using System;
using RommStar.Core.Extensions;
using RommStar.Core.Models;
using Unbroken.LaunchBox.Plugins.Data;

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

        /// <summary>
        /// NOTE: Essentially redundant presently, but kept in in case of future development (e.g. bios downloads). 
        /// </summary>
        public MediaType? MediaType { get; set; }
        public string RomName { get; set; }
        public string RelativeUrl { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public string LaunchBoxPlatformName { get; set; } = string.Empty;
        public RommServer ServerContext { get; set; } = null!;
        public PlatformSyncCardVM? UiCard { get; set; } // Null if bypassed via on-demand
        public Action? OnSuccessCallback { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public IGame IGame { get; set; }

        public string ToCsv(bool redact = false)
        {
            return $"[{IGame.Title} ({LaunchBoxPlatformName})] ({JobType}: {MediaType}): " +
              $"Server: [{ServerContext.ServerName}]. " +
              $"URL: [{RelativeUrl.RedactSensitiveInfo(redact)}]. " +
              $"Destination: [{DestinationPath}]";
        }


    }
}