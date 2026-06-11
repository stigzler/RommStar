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
    [DefaultValue(SyncProfile.CreateGame_DownloadMedia)]
    public enum SyncProfile
    {
        [CustomName("Create Game, Download Media [Default]")]
        [Description("Create Game in launchbox and get media from romm server. Best with auto-import off?")]
        CreateGame_DownloadMedia,

        [CustomName("Create Game, Download Rom, Download Media")]
        [Description("Create IGame in launchbox and get the rom and media from romm server. Auto-import needs to be off?")]
        CreateGame_DownloadRom_DownloadMedia,

        [CustomName("Create Game, Download Rom")]
        [Description("Create IGame in launchbox and get the rom from romm server. Auto-import needs to be off?")]
        CreateGame_DownloadRom,

        [CustomName("Create Game")]
        [Description("Create Game only. Best with auto-import off?")]
        CreateGame,

        [CustomName("Download Rom")]
        [Description("Download Rom only. Can leave auto-import on?")]
        DownloadRom
    }
}