using RommStar.Core.Sync;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Models
{
    /// <summary>
    /// ALL Properties should be nullable.
    /// Null in a PLatform-specific SyncProfileTypes will cause it to be ignored
    /// and the default global setting used.
    /// </summary>
    public class ExtendedSyncSettings
    {
        public bool ApplySettings { get; set; } = false;
        public SyncProfileTypes SyncProfile { get; set; } = SyncProfileTypes.CreateGame_DownloadMedia;
        public bool OverwriteMetadata { get; set; } = true;

        public bool RemoveLocalGamesNotOnRommServer { get; set; } = false;
    }
}