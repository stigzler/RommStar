using RommStar.Core.Models;
using RommStar.Core.Primitives;
using RommStar.Core.Sync;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Properties
{
    public class PluginSettings
    {
        public bool DarkModeEnabled { get; set; } = true;

        public ExtendedSyncSettings GlobalExtendedSyncSettings { get; set; } = new()
        {
            SyncProfile = SyncProfileTypes.UpdateMetadata_DownloadMedia
        };

        /// <summary>
        /// Default for Sync Jobs: hides success log entries if true.
        /// </summary>
        public bool HideSuccessEntries { get; set; } = true;

        /// <summary>
        /// Selected media items to pull when performing an on-demand game installation.
        /// </summary>
        public MediaSelectionProfile InstallMediaProfile { get; set; } = new();

        /// <summary>
        /// RommStar logging level
        /// </summary>
        public LoggingLevel LoggingLevel { get; set; } = LoggingLevel.Verbose;

        public bool LoggingRedact { get; set; } = true;

        public bool LoggingIncludeMember { get; set; } = true;

        public bool LoggingPrependMember { get; set; } = true;

        public List<PlatformSyncSettings> PlatformSyncSettings { get; set; } = new();

        /// <summary>
        /// In lieu of future development letting you choose your preferred standard. Not in UI
        /// </summary>
        public RatingStandard RatingStandard { get; set; } = RatingStandard.ESRB;



        /// <summary>
        /// User-set space for temporary downloads. Eg: when the Romm API 
        /// downloads the zipped rom collection files to.
        /// </summary>
        public ObservableCollection<RomQueueItem> RomDownloadQueue { get; set; } = new();

        /// <summary>
        /// Store User set Romm Server details
        /// </summary>
        public List<RommServer> RommServers { get; set; } = new();
        /// <summary>
        /// Selected media items to pull during background metadata synchronizations.
        /// </summary>
        public MediaSelectionProfile SyncMediaProfile { get; set; } = new();

        public string YouTubeStub { get; set; } = "https://www.youtube.com/watch?v=";
    }
}