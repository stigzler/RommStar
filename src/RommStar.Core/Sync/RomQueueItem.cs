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
        /// <summary>
        /// If rom download fails, this counts up to set limit.
        /// </summary>
        public int RetryCount { get; set; } = 0;

        /// <summary>
        /// Excludes rom from further download batching until user removes the quarentine. 
        /// </summary>
        public bool IsQuarantined { get; set; } = false;

        /// <summary>
        /// HOlds any errors experienced during the rom download
        /// </summary>
        public string LastError { get; set; } = string.Empty;

        public DateTime AddedAt { get; set; } = DateTime.Now;
        /// <summary>
        /// Sanitised = stripped of illegal path chars
        /// </summary>
        public string GameNameSanitised { get; set; }
        public bool IsMultiFileGame { get; set; } = false;
        public bool IsPriority { get; set; } = false;

        /// <summary>
        /// The local Id - not lb db id.
        /// </summary>
        public string LaunchboxId { get; set; } = string.Empty;

        /// <summary>
        /// This accommodates sibling/disc set romsets for games. 
        /// E.g. for games that have 'siblings' (eg. versions of the same game), MasterFilename will be the one
        /// with IsMainSibling set to true in  romm. Will choose 1st disc etc for disc sets. 
        /// </summary>
        public string MasterFilename { get; set; } = string.Empty;
        public List<RomFileDTO>? MultiFiles { get; set; } = new();
        public bool NotifyLaunchboxOnCompletion { get; set; } = false;
        public string PlatformName { get; set; } = string.Empty;
        public string PlatformStub { get; set; } = string.Empty;

        /// <summary>
        /// Holds all RomM IDs required for this specific game (siblings, discs, etc.)
        /// </summary>
        public List<int> RommIds { get; set; } = new();
        public string ServerId { get; set; } = string.Empty;
        public long TotalSizeBytes { get; set; }
    }
}
