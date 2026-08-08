using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using RommStar.Core.CustomAttributes;

namespace RommStar.Core.Sync
{
    [DefaultValue(SyncProfileTypes.UpdateMetadata_DownloadMedia)]
    public enum SyncProfileTypes
    {
        [CustomName("Update Metadata, Download Media [Default]")]
        [Description("Update Metadata in launchbox and get media from romm server. Best with auto-import off?")]
        UpdateMetadata_DownloadMedia,

        [CustomName("Update Metadata, Download Rom, Download Media")]
        [Description("Update Metadata in launchbox and get the rom and media from romm server. Auto-import needs to be off?")]
        UpdateMetadata_DownloadRom_DownloadMedia,

        [CustomName("Update Metadata, Download Rom")]
        [Description("Update Metadata in launchbox and get the rom from romm server. Auto-import needs to be off?")]
        UpdateMetadata_DownloadRom,

        [CustomName("Update Metadata")]
        [Description("Update Metadata only. Best with auto-import off?")]
        UpdateMetadata,

        [CustomName("Download Rom")]
        [Description("Download Rom only. Can leave auto-import on?")]
        DownloadRom
    }
}