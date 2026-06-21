using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Sync
{
    public class RomQueueItem
    {
        public string LaunchboxId { get; set; } = string.Empty;

        // Holds all RomM IDs required for this specific game (siblings, discs, etc.)
        public List<int> RommIds { get; set; } = new();

        public string PlatformName { get; set; } = string.Empty;
        public long TotalSizeBytes { get; set; }

        // Prioritization flags
        public bool IsPriority { get; set; } = false;
        public DateTime AddedAt { get; set; } = DateTime.Now;
    }
}
