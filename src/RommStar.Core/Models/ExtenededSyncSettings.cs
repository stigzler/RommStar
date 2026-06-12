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
    /// Null in a PLatform-specific SyncProfile will cause it to be ignored
    /// and the default global setting used.
    /// </summary>
    public class ExtendedSyncSettings
    {
        public bool ApplySettings { get; set; } = false;
        public SyncProfile SyncProfile { get; set; } = SyncProfile.CreateGame_DownloadMedia;

    }
}