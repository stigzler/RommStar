using CommunityToolkit.Mvvm.ComponentModel;
using RommStar.Core.Primitives;
using RommStar.Core.Sync;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Models
{
    // Todo: refactor this for proper MVVM - this is UI operating directly on the model. 
    /// <summary>
    /// ALL Properties should be nullable.
    /// Null in a PLatform-specific SyncProfileTypes will cause it to be ignored
    /// and the default global setting used.
    /// </summary>
    public class ExtendedSyncSettings
    {
        public bool ApplySettings = false;
        public SyncProfileTypes SyncProfile { get; set; } = SyncProfileTypes.UpdateMetadata_DownloadMedia;
        public bool OverwriteMetadata { get; set; } = true;
        public bool OverwriteExistingMedia { get; set; } = true;
        public bool OverwriteExistingRoms { get; set; } = true;
        public bool ForceMediaPriority { get; set; } = true;
        public string TempDownloadsPath { get; set; } = "TemporaryDownloads";
        public long TargetRomBatchFilesizeGb { get; set; } = 2; // 2GB
        public bool UseIndividualGameFolders { get; set; } = false;

        /// <summary>
        /// When processing metadata and checking if local rom exists, the method to use (SHA1 slower)
        /// </summary>
        public FileCheckMethod FileCheckMethod { get; set; } = FileCheckMethod.FileOnly;

        /// <summary>
        /// If local launchbox platform contains roms from a previously assigned server, these are deleted. 
        /// </summary>
        public bool DeleteOldServerRoms { get; set; } = false;
    }
}