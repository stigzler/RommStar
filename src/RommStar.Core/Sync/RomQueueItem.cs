using Microsoft.Xaml.Behaviors.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using RommStar.Core.Dtos.Romm;

namespace RommStar.Core.Sync
{
    public class RomQueueItem
    {
        public DateTime AddedAt { get; set; } = DateTime.Now;
        public bool IsPriority { get; set; } = false;

        /// <summary>
        /// The local Id - not lb db id.
        /// </summary>
        public string LaunchboxId { get; set; } = string.Empty;
        public string PlatformName { get; set; } = string.Empty;
        public string PlatformStub { get; set; } = string.Empty;
        public string ServerId { get; set; } = string.Empty;
        public bool NotifyLaunchboxOnCompletion { get; set; } = false;
        public List<RomFileDTO>? MultiFiles { get; set; } = new();

        /// <summary>
        /// This accommodates sibling/disc set romsets for games. 
        /// E.g. for games that have 'siblings' (eg. versions of the same game), MasterFilename will be the one
        /// with IsMainSibling set to true in  romm. Will choose 1st disc etc for disc sets. 
        /// </summary>
        public string MasterFilename { get; set; } = string.Empty;

        public bool IsMultiFileGame { get; set; } = false;

        /// <summary>
        /// Holds all RomM IDs required for this specific game (siblings, discs, etc.)
        /// </summary>
        public List<int> RommIds { get; set; } = new();
        public long TotalSizeBytes { get; set; }

        /// <summary>
        /// Sanitised = stripped of illegal path chars
        /// </summary>
        public string GameNameSanitised { get; set; }

    }
}
