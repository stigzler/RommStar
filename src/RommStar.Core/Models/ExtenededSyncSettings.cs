using CommunityToolkit.Mvvm.ComponentModel;
using RommStar.Core.Extensions;
using RommStar.Core.Primitives;
using RommStar.Core.Sync;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
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
        public bool ApplySettings { get; set; } = false;
        public SyncProfileTypes SyncProfile { get; set; } = SyncProfileTypes.UpdateMetadata_DownloadMedia;
        public bool OverwriteMetadata { get; set; } = true;
        public bool OverwriteExistingMedia { get; set; } = true;
        public bool OverwriteExistingRoms { get; set; } = false;
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

        public string ToCsv()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{SyncProfile.ToString()}, ");
            if (OverwriteMetadata ) sb.Append($"Overwrite Metadata, ");
            if (OverwriteExistingMedia) sb.Append($"Overwrite Existing Media, ");
            if (OverwriteExistingRoms) sb.Append($"Overwrite Existing Roms, ");
            if (ForceMediaPriority) sb.Append($"Force Media Priority, ");
            sb.Append($"Batch Size: {TargetRomBatchFilesizeGb} (GB), ");
            sb.Append($"File Check: {FileCheckMethod}, ");
            sb.Append($"Temp Downloads Path: [{TempDownloadsPath}]");
            return sb.ToString();
        }

   

    }
}