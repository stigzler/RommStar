using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Models
{
    public class PlatformSyncSettings
    {
        /// <summary>
        /// Uses IPlatform.Name
        /// </summary>
        public string LaunchboxPlatformName { get; set; }

        public Guid RommServerId { get; set; }

        public List<int> RommServerPlatforms { get; set; }
    }
}