using Microsoft.Xaml.Behaviors.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Sync
{
    public class RomQueueItem
    {
        public DateTime AddedAt { get; set; } = DateTime.Now;
        /// <summary>
        /// Prioritization flags
        /// </summary>
        public bool IsPriority { get; set; } = false;

        public string LaunchboxId { get; set; } = string.Empty;

        public string PlatformName { get; set; } = string.Empty;

        public string PlatformStub { get; set; } = string.Empty;

        /// <summary>
        /// Holds all RomM IDs required for this specific game (siblings, discs, etc.)
        /// </summary>
        public List<int> RommIds { get; set; } = new();
        public long TotalSizeBytes { get; set; }

        public string GameNameSanitised { get; set; }


    }
}
